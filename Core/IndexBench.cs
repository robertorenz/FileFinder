using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace FileFinder.Core;

/// <summary>
/// Measures where indexing time actually goes, so we can tell whether a
/// hand-written assembly path would help. Runnable via
/// <c>FileFinder.exe --benchindex [path]</c>.
///
/// It separates three phases on real files:
///   1. Enumeration   - disk + FindNextFile syscalls (NOT assembly-accelerable)
///   2. Full build    - normalize + directory de-dup + blob packing
///   3. Normalize-only - the isolated lower-case + UTF-8 kernel, which is the
///                       only CPU-bound, SIMD/assembly-accelerable slice.
/// </summary>
internal static class IndexBench
{
    [DllImport("kernel32.dll")] private static extern bool AttachConsole(int pid);
    [DllImport("kernel32.dll")] private static extern bool AllocConsole();

    private static readonly EnumerationOptions Opts = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
        ReturnSpecialDirectories = false
    };

    public static int Run(string? path)
    {
        if (!AttachConsole(-1)) AllocConsole();

        path ??= Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!Directory.Exists(path))
        {
            Console.WriteLine($"\nPath not found: {path}\n");
            return 1;
        }

        Console.WriteLine();
        Console.WriteLine($"Index benchmark on:  {path}");
        Console.WriteLine("(warming the file-system cache first so the split reflects steady state)\n");

        // Warm pass (prime the OS directory cache) so the timed enumeration
        // reflects the CPU/IO balance rather than a one-off cold read.
        foreach (var _ in Directory.EnumerateFiles(path, "*", Opts)) { }

        // ---- Phase 1: enumeration only (disk + syscalls) ----
        var names = new List<string>(1 << 16);
        var dirs = new List<string>(1 << 16);
        var sw = Stopwatch.StartNew();
        foreach (string p in Directory.EnumerateFiles(path, "*", Opts))
        {
            int sep = p.LastIndexOf('\\');
            dirs.Add(sep > 0 ? p.Substring(0, sep) : p);
            names.Add(p.Substring(sep + 1));
        }
        sw.Stop();
        double enumMs = sw.Elapsed.TotalMilliseconds;
        int n = names.Count;

        // ---- Phase 2: full index build (normalize + dedup + pack) ----
        sw.Restart();
        var b = new IndexBuilder();
        for (int i = 0; i < n; i++) b.Add(dirs[i], names[i].AsSpan());
        b.Build(new[] { path }, DateTime.UtcNow);
        sw.Stop();
        double buildMs = sw.Elapsed.TotalMilliseconds;

        // ---- Phase 3: isolated lower-case + UTF-8 kernel (asm-accelerable) ----
        var scratchChars = new char[512];
        var scratchBytes = new byte[2048];
        long totalBytes = 0;
        sw.Restart();
        for (int i = 0; i < n; i++)
        {
            string name = names[i];
            if (scratchChars.Length < name.Length) scratchChars = new char[name.Length];
            if (scratchBytes.Length < name.Length * 3) scratchBytes = new byte[name.Length * 3];
            for (int j = 0; j < name.Length; j++) scratchChars[j] = char.ToLowerInvariant(name[j]);
            totalBytes += Encoding.UTF8.GetBytes(scratchChars.AsSpan(0, name.Length), scratchBytes);
        }
        sw.Stop();
        double normMs = sw.Elapsed.TotalMilliseconds;

        // ---- report ----
        double total = enumMs + buildMs; // representative end-to-end (build includes normalize)
        Console.WriteLine($"Files found:            {n:N0}");
        Console.WriteLine($"Name bytes normalized:  {totalBytes:N0}");
        Console.WriteLine();
        Console.WriteLine($"  Phase 1  enumeration (disk + syscalls):  {enumMs,8:0.0} ms   {Pct(enumMs, total)}");
        Console.WriteLine($"  Phase 2  full index build:               {buildMs,8:0.0} ms   {Pct(buildMs, total)}");
        Console.WriteLine($"           (end-to-end index ~= P1 + P2):  {total,8:0.0} ms");
        Console.WriteLine();
        Console.WriteLine($"  Of which the assembly-accelerable part:");
        Console.WriteLine($"  Phase 3  lower-case + UTF-8 kernel only:  {normMs,8:0.0} ms   {Pct(normMs, total)} of total");
        Console.WriteLine();

        double share = total > 0 ? normMs / total * 100 : 0;
        Console.WriteLine(share < 20
            ? $"VERDICT: name normalization is only ~{share:0}% of indexing — even a 3x asm speedup"
              + $" would cut total index time by under {share * 2 / 3:0}%. Indexing is disk/OS-bound."
            : $"VERDICT: name normalization is ~{share:0}% of indexing — an asm kernel could move the needle.");
        Console.WriteLine();
        return 0;
    }

    private static string Pct(double part, double whole)
        => whole > 0 ? $"({part / whole * 100,4:0.0}%)" : "";
}
