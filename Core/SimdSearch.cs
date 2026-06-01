using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace FileFinder.Core;

/// <summary>
/// The hot search loop. This is the "assembly" the app is built around: the
/// substring matcher below is written with AVX2 hardware intrinsics, which the
/// JIT lowers directly to vectorized machine instructions (vpcmpeqb / vpmovmskb
/// / tzcnt) — the exact same opcodes you would hand-write in a .asm file, but
/// without a separate native toolchain.
///
/// Strategy: every filename is stored once, lower-cased, as UTF-8 bytes inside
/// one big contiguous blob (see <see cref="FileIndex"/>). To test a name we
/// broadcast the needle's first byte across a 256-bit register, compare 32 name
/// bytes at a time, and only fall back to a full compare on the handful of
/// positions where the first byte actually matched.
/// </summary>
public static unsafe class SimdSearch
{
    /// <summary>True if the CPU offers the fast vectorized path.</summary>
    public static bool HardwareAccelerated => Avx2.IsSupported;

    /// <summary>
    /// Returns true if <paramref name="needle"/> occurs anywhere inside the
    /// name stored at blob[start .. start+len). The blob is padded with 32
    /// slack bytes at the end so the 32-byte loads near the tail never read out
    /// of bounds.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static bool Contains(byte* blob, int start, int len, byte* needle, int nlen)
    {
        if (nlen == 0) return true;
        if (nlen > len) return false;

        int last = len - nlen; // last valid start offset within the name

        if (Avx2.IsSupported && len >= 16)
        {
            Vector256<byte> first = Vector256.Create(needle[0]);
            byte* p = blob + start;
            int i = 0;

            // Walk the name 32 bytes at a time.
            for (; i <= last; i += 32)
            {
                Vector256<byte> block = Avx.LoadVector256(p + i);
                uint mask = (uint)Avx2.MoveMask(Avx2.CompareEqual(block, first));

                // Each set bit is a position where the first needle byte matched.
                while (mask != 0)
                {
                    int bit = BitOperations.TrailingZeroCount(mask);
                    int pos = i + bit;
                    if (pos > last) break;          // remaining bits are further right
                    if (EqualsAt(p + pos, needle, nlen)) return true;
                    mask &= mask - 1;               // clear lowest set bit
                }
            }
            return false;
        }

        // Scalar fallback for tiny strings or non-AVX2 CPUs.
        return ScalarContains(blob + start, last, needle, nlen);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool EqualsAt(byte* a, byte* b, int n)
    {
        int i = 0;
        // Compare 16 bytes at a time when the needle is long enough.
        if (Sse2.IsSupported)
        {
            for (; i + 16 <= n; i += 16)
            {
                Vector128<byte> va = Sse2.LoadVector128(a + i);
                Vector128<byte> vb = Sse2.LoadVector128(b + i);
                if ((uint)Sse2.MoveMask(Sse2.CompareEqual(va, vb)) != 0xFFFF) return false;
            }
        }
        for (; i < n; i++)
            if (a[i] != b[i]) return false;
        return true;
    }

    private static bool ScalarContains(byte* name, int last, byte* needle, int nlen)
    {
        byte n0 = needle[0];
        for (int pos = 0; pos <= last; pos++)
        {
            if (name[pos] != n0) continue;
            if (EqualsAt(name + pos, needle, nlen)) return true;
        }
        return false;
    }
}
