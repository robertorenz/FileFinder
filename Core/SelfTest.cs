using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace FileFinder.Core;

/// <summary>
/// Lightweight correctness + performance check for the index and SIMD search,
/// runnable via `FileFinder.exe --selftest`. Writes results to an attached
/// console. Not part of the GUI flow.
/// </summary>
internal static class SelfTest
{
    [DllImport("kernel32.dll")] private static extern bool AttachConsole(int pid);
    [DllImport("kernel32.dll")] private static extern bool AllocConsole();

    public static int Run()
    {
        if (!AttachConsole(-1)) AllocConsole();
        int failures = 0;

        void Check(string label, bool ok)
        {
            Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {label}");
            if (!ok) failures++;
        }

        Console.WriteLine();
        Console.WriteLine($"FileFinder self-test  (AVX2 hardware path: {SimdSearch.HardwareAccelerated})");

        // ---- build a small, varied index ----
        var b = new IndexBuilder();
        string[][] data =
        {
            new[] { @"C:\Windows\System32", "kernel32.dll" },
            new[] { @"C:\Windows\System32", "KERNELBASE.dll" },
            new[] { @"C:\Users\me\Documents", "Quarterly Report 2024.xlsx" },
            new[] { @"C:\Users\me\Pictures", "vacation-café-México.JPG" },
            new[] { @"C:\Users\me\Pictures", "loop-Animation.GIF" },
            new[] { @"C:\dev\proj", "Program.cs" },
            new[] { @"C:\dev\proj", "README.md" },
            new[] { @"C:\dev\proj\bin", "app.exe" },
            new[] { @"C:\temp", "a.txt" },
        };
        foreach (var row in data) b.Add(row[0], row[1].AsSpan());
        var index = b.Build(new[] { @"C:\" }, DateTime.UtcNow);

        Check("count matches", index.Count == data.Length);

        // ---- substring / case-insensitive matching ----
        Check("'kernel' -> 2 (case-insensitive)", index.Search("kernel", 100).total == 2);
        Check("'.dll' -> 2", index.Search(".dll", 100).total == 2);
        Check("'report' mid-string + case", index.Search("report", 100).total == 1);
        Check("'.cs' exact-tail", index.Search(".cs", 100).total == 1);
        Check("unicode 'café' matches", index.Search("café", 100).total == 1);
        Check("unicode 'MÉXICO' upper matches", index.Search("MÉXICO", 100).total == 1);
        Check("no false positive", index.Search("zzzznope", 100).total == 0);

        // ---- wildcard / glob matching ----
        Check("'*.dll' -> 2 (ends-with)", index.Search("*.dll", 100).total == 2);
        Check("'*.gif' upper-ext matches", index.Search("*.gif", 100).total == 1);
        Check("'kernel*' starts-with -> 2", index.Search("kernel*", 100).total == 2);
        Check("'*report*' contains via glob", index.Search("*report*", 100).total == 1);
        Check("'*.cs' anchored (not README)", index.Search("*.cs", 100).total == 1);
        Check("'?.txt' single-char wildcard", index.Search("?.txt", 100).total == 1);
        Check("'*' matches everything", index.Search("*", 100).total == data.Length);
        Check("'*.xyz' no match", index.Search("*.xyz", 100).total == 0);
        Check("'program.cs' no wildcard still works", index.Search("program.cs", 100).total == 1);
        Check("single char 'a' broad", index.Search("a", 100).total >= 4);
        Check("limit caps hits but not total", index.Search("a", 2).hits.Count == 2);

        // ---- full-path reconstruction ----
        var (hits, _) = index.Search("Program.cs", 10);
        Check("full path rebuilt",
            hits.Count == 1 && index.GetFullPath(hits[0]) == @"C:\dev\proj\Program.cs");

        // ---- cache round-trip ----
        string tmp = Path.Combine(Path.GetTempPath(), "fffix_selftest.ffix");
        index.Save(tmp);
        var loaded = FileIndex.TryLoad(tmp);
        Check("cache reloads",
            loaded != null && loaded.Count == index.Count &&
            loaded.Search("kernel", 100).total == 2 &&
            loaded.GetFullPath(0) == index.GetFullPath(0));
        try { File.Delete(tmp); } catch { }

        // ---- correctness vs. naive search on a large synthetic set ----
        var big = new IndexBuilder();
        for (int i = 0; i < 200_000; i++)
            big.Add(@"C:\gen", $"file_{i}_data_{(i % 97)}.bin".AsSpan());
        var bigIndex = big.Build(new[] { @"C:\" }, DateTime.UtcNow);

        // naive reference count
        int naive = 0;
        for (int i = 0; i < bigIndex.Count; i++)
            if (bigIndex.GetName(i).ToLowerInvariant().Contains("data_42")) naive++;

        var sw = Stopwatch.StartNew();
        var simd = bigIndex.Search("data_42", int.MaxValue);
        sw.Stop();
        Check($"SIMD == naive on 200k ({simd.total} == {naive})", simd.total == naive);
        Console.WriteLine($"  ...searched {bigIndex.Count:N0} names in {sw.Elapsed.TotalMilliseconds:0.0} ms");

        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "ALL TESTS PASSED" : $"{failures} TEST(S) FAILED");
        Console.WriteLine();
        return failures == 0 ? 0 : 1;
    }
}
