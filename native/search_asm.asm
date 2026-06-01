; ============================================================================
; search_asm.asm  -  Hand-written x64 AVX2 substring search over a packed,
;                    lower-cased file-name blob. Built with ml64.exe + link.exe
;                    into FileFinderAsm.dll and P/Invoked from C#.
;
; This is the literal-assembly counterpart to the JIT/intrinsics matcher in
; Core/SimdSearch.cs, so the two engines can be benchmarked head to head on
; identical data.
;
; Exported function (Win64 calling convention, single pointer arg in RCX):
;
;   long asm_search_range(SearchArgs* a)
;
;   struct SearchArgs (offsets in bytes):
;      0   blob      const uint8*   packed lower-cased names (+32 slack bytes)
;      8   offs      const int32*   per-file start offsets, length count+1
;      16  needle    const uint8*   lower-cased query bytes
;      24  nlen      int32          query length
;      28  from      int32          first file index (inclusive)
;      32  to        int32          last file index (exclusive)
;      36  maxHits   int32          capacity of outHits
;      40  outHits   int32*         receives matching file indices (capped)
;      48  outCount  int32*         receives number of indices written
;
;   returns: total number of matches (may exceed maxHits) in RAX
; ============================================================================

.code

asm_search_range PROC
    push rbx
    push rsi
    push rdi
    push r12
    push r13
    push r14
    push r15
    push rbp
    sub  rsp, 16                  ; locals: [rsp]=total(qword), [rsp+8]=hits(dword)

    mov  rbp, rcx                 ; rbp = args
    mov  rsi, [rbp]               ; rsi = blob
    mov  rdi, [rbp+8]             ; rdi = offs
    mov  rbx, [rbp+16]            ; rbx = needle
    mov  r10d, [rbp+24]           ; r10d = nlen
    mov  r8d, [rbp+28]            ; r8d  = i (from)
    mov  r9d, [rbp+32]            ; r9d  = to

    xor  rax, rax
    mov  [rsp], rax               ; total = 0
    mov  dword ptr [rsp+8], 0     ; hits  = 0

    movzx eax, byte ptr [rbx]     ; first needle byte
    vmovd xmm0, eax
    vpbroadcastb ymm0, xmm0       ; ymm0 = first byte broadcast across 32 lanes

file_loop:
    cmp  r8d, r9d
    jge  done
    mov  eax, [rdi + r8*4]        ; start = offs[i]
    mov  edx, [rdi + r8*4 + 4]    ; offs[i+1]
    sub  edx, eax                 ; len = offs[i+1]-start
    cmp  r10d, edx
    jg   next_file                ; nlen > len -> cannot match
    lea  r12, [rsi + rax]         ; p = blob + start
    mov  r13d, edx
    sub  r13d, r10d               ; last = len - nlen  (>= 0)
    xor  ecx, ecx                 ; j = 0

scan_block:
    vmovdqu ymm1, ymmword ptr [r12 + rcx]
    vpcmpeqb ymm1, ymm1, ymm0
    vpmovmskb eax, ymm1           ; eax = 32-bit first-byte match mask

bit_loop:
    test eax, eax
    jz   advance_block
    blsr r14d, eax                ; r14d = mask with lowest set bit cleared
    tzcnt edx, eax                ; edx = bit index
    add  edx, ecx                 ; pos = j + bit
    cmp  edx, r13d
    jg   advance_block            ; pos > last; remaining bits are higher
    lea  r11, [r12 + rdx]         ; candidate = p + pos
    xor  r15d, r15d               ; k = 0
verify_loop:
    cmp  r15d, r10d
    je   matched                  ; matched all needle bytes
    mov  al, [r11 + r15]
    cmp  al, [rbx + r15]
    jne  verify_fail
    inc  r15d
    jmp  verify_loop
verify_fail:
    mov  eax, r14d                ; restore remaining mask, try next bit
    jmp  bit_loop

advance_block:
    add  ecx, 32
    cmp  ecx, r13d
    jle  scan_block
    jmp  next_file                ; no match in this name

matched:
    mov  rax, [rsp]
    inc  rax
    mov  [rsp], rax               ; total++
    mov  eax, [rsp+8]             ; hits
    cmp  eax, [rbp+36]            ; maxHits
    jge  next_file                ; capped: count but do not store
    mov  r11, [rbp+40]            ; outHits
    mov  [r11 + rax*4], r8d       ; outHits[hits] = i
    inc  eax
    mov  [rsp+8], eax             ; hits++

next_file:
    inc  r8d
    jmp  file_loop

done:
    mov  rax, [rbp+48]            ; outCount
    mov  edx, [rsp+8]
    mov  [rax], edx               ; *outCount = hits
    mov  rax, [rsp]               ; return total
    vzeroupper
    add  rsp, 16
    pop  rbp
    pop  r15
    pop  r14
    pop  r13
    pop  r12
    pop  rdi
    pop  rsi
    pop  rbx
    ret
asm_search_range ENDP

END
