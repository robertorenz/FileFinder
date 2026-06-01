using System.Collections.Generic;
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

        // ---- multi-word (AND, any order, anywhere incl. extension) ----
        Check("'report 2024' -> 1 (all words)", index.Search("report 2024", 100).total == 1);
        Check("'2024 report' order-independent", index.Search("2024 report", 100).total == 1);
        Check("'animation gif' matches name+ext", index.Search("animation gif", 100).total == 1);
        Check("'report 2024 missing' -> 0", index.Search("report 2024 missing", 100).total == 0);
        Check("'kernel dll' both words -> 2", index.Search("kernel dll", 100).total == 2);
        Check("extra spaces ignored", index.Search("  report   2024  ", 100).total == 1);

        // ---- statistics helpers ----
        Check("directory count (6 unique)", index.DirectoryCount == 6);
        var topExt = index.TopExtensions(5);
        Check("top extension is .dll x2",
            topExt.Count > 0 && topExt[0].Ext == ".dll" && topExt[0].Count == 2);
        Check("memory estimate > 0", index.ApproxMemoryBytes > 0);

        // ---- localization completeness: Spanish covers every English key ----
        int missingEs = 0; string firstMissing = "";
        foreach (var key in FileFinder.Localization.Strings.En.Keys)
            if (!FileFinder.Localization.Strings.Es.ContainsKey(key))
            {
                missingEs++;
                if (firstMissing.Length == 0) firstMissing = key;
            }
        Check($"Spanish covers all {FileFinder.Localization.Strings.En.Count} keys" +
              (missingEs > 0 ? $" (missing {missingEs}, e.g. {firstMissing})" : ""), missingEs == 0);

        // ---- file-row metadata (size / modified / attributes) ----
        string metaTmp = Path.Combine(Path.GetTempPath(), "fffix_meta_test.bin");
        File.WriteAllBytes(metaTmp, new byte[4096]);
        var metaRow = new FileFinder.Models.FileRow
        {
            Name = Path.GetFileName(metaTmp),
            Directory = Path.GetDirectoryName(metaTmp)!,
            FullPath = metaTmp
        };
        metaRow.LoadMetadata();
        Check("file metadata: size populated", metaRow.SizeText.Length > 0);
        Check("file metadata: modified populated", metaRow.ModifiedText.Length > 0);
        try { File.Delete(metaTmp); } catch { }
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

        // ---- MASM engine: correctness vs JIT + head-to-head timing ----
        Console.WriteLine();
        if (NativeSearch.IsAvailable)
        {
            var jit = bigIndex.Search("data_42", int.MaxValue, SearchEngine.Jit);
            var asm = bigIndex.Search("data_42", int.MaxValue, SearchEngine.Masm);
            Check($"MASM total == JIT total ({asm.total} == {jit.total})", asm.total == jit.total);
            Check("MASM hit set == JIT hit set",
                new HashSet<int>(asm.hits).SetEquals(new HashSet<int>(jit.hits)));

            double jitMs = BestOf(() => bigIndex.Search("data_42", int.MaxValue, SearchEngine.Jit), 50);
            double asmMs = BestOf(() => bigIndex.Search("data_42", int.MaxValue, SearchEngine.Masm), 50);
            Console.WriteLine($"  Benchmark over {bigIndex.Count:N0} names (best of 50):");
            Console.WriteLine($"    JIT  (AVX2 intrinsics): {jitMs:0.000} ms");
            Console.WriteLine($"    MASM (FileFinderAsm.dll): {asmMs:0.000} ms");
        }
        else
        {
            Console.WriteLine("  MASM engine: NOT available (FileFinderAsm.dll missing) — skipped.");
        }

        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "ALL TESTS PASSED" : $"{failures} TEST(S) FAILED");
        Console.WriteLine();
        return failures == 0 ? 0 : 1;
    }

    /// <summary>Runs <paramref name="action"/> <paramref name="iters"/> times and returns the fastest in ms.</summary>
    private static double BestOf(Action action, int iters)
    {
        action(); // warm up
        double best = double.MaxValue;
        var sw = new Stopwatch();
        for (int i = 0; i < iters; i++)
        {
            sw.Restart();
            action();
            sw.Stop();
            double ms = sw.Elapsed.TotalMilliseconds;
            if (ms < best) best = ms;
        }
        return best;
    }
}
