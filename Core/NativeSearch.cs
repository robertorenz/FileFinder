using System.Runtime.InteropServices;

namespace FileFinder.Core;

/// <summary>
/// P/Invoke bridge to the hand-written MASM routine in
/// <c>native/search_asm.asm</c> (built into <c>FileFinderAsm.dll</c>). Mirrors
/// the managed AVX2 matcher in <see cref="SimdSearch"/> so the two can be raced.
/// If the DLL is missing or the CPU lacks AVX2/BMI, <see cref="IsAvailable"/>
/// is false and callers fall back to the JIT engine.
/// </summary>
public static unsafe class NativeSearch
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SearchArgs
    {
        public IntPtr Blob;      // 0  const uint8*
        public IntPtr Offs;      // 8  const int32*
        public IntPtr Needle;    // 16 const uint8*
        public int Nlen;         // 24
        public int From;         // 28
        public int To;           // 32
        public int MaxHits;      // 36
        public IntPtr OutHits;   // 40 int32*
        public IntPtr OutCount;  // 48 int32*
    }

    [DllImport("FileFinderAsm.dll", ExactSpelling = true)]
    public static extern long asm_search_range(ref SearchArgs args);

    private static readonly Lazy<bool> _available = new(Probe);

    /// <summary>True if the native DLL loaded and returns correct results.</summary>
    public static bool IsAvailable => _available.Value;

    private static bool Probe()
    {
        try
        {
            // blob = "abcabc" + slack; search "bc" over two files: "abc","abc"
            byte[] blob = new byte[6 + FileIndex.SlackBytes];
            byte[] src = { (byte)'a', (byte)'b', (byte)'c', (byte)'a', (byte)'b', (byte)'c' };
            Array.Copy(src, blob, src.Length);
            int[] offs = { 0, 3, 6 };
            byte[] needle = { (byte)'b', (byte)'c' };
            int[] outHits = new int[4];

            fixed (byte* b = blob)
            fixed (int* o = offs)
            fixed (byte* nd = needle)
            fixed (int* h = outHits)
            {
                int count = 0;
                var args = new SearchArgs
                {
                    Blob = (IntPtr)b,
                    Offs = (IntPtr)o,
                    Needle = (IntPtr)nd,
                    Nlen = needle.Length,
                    From = 0,
                    To = 2,
                    MaxHits = 4,
                    OutHits = (IntPtr)h,
                    OutCount = (IntPtr)(&count)
                };
                long total = asm_search_range(ref args);
                // Both files contain "bc": expect total 2, two indices 0 and 1.
                return total == 2 && count == 2 && outHits[0] == 0 && outHits[1] == 1;
            }
        }
        catch
        {
            return false;
        }
    }
}
