namespace FileFinder.Core;

/// <summary>Which substring-search implementation to use.</summary>
public enum SearchEngine
{
    /// <summary>C# AVX2 hardware intrinsics, JIT-compiled to SIMD (Core/SimdSearch.cs).</summary>
    Jit,

    /// <summary>Hand-written MASM x64 routine in FileFinderAsm.dll (native/search_asm.asm).</summary>
    Masm
}
