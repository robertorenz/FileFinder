using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileFinder.Core;

namespace FileFinder.Indexing;

/// <summary>
/// Portable, no-admin indexer. Parallelizes by handing each top-level folder of
/// a drive to its own worker, which recursively enumerates files using the fast
/// Win32-backed .NET enumerator (inaccessible folders are skipped silently).
/// Each worker fills a private <see cref="IndexBuilder"/>; results are merged at
/// the end so the hot loop stays lock-free.
/// </summary>
public static class DirectoryWalker
{
    private static readonly EnumerationOptions FileOpts = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint, // avoid symlink/junction loops
        ReturnSpecialDirectories = false
    };

    private static readonly EnumerationOptions TopDirOpts = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
        ReturnSpecialDirectories = false
    };

    public static void Index(string driveRoot, IndexBuilder target, object mergeLock,
                             Action<long, string> onProgress, CancellationToken ct)
    {
        // Top-level work units: every immediate subdirectory + the root itself.
        var roots = new List<string> { driveRoot };
        try
        {
            roots.AddRange(Directory.EnumerateDirectories(driveRoot, "*", TopDirOpts));
        }
        catch { /* drive not ready / access denied at root */ }

        long total = 0;

        Parallel.ForEach(roots,
            new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = Environment.ProcessorCount },
            () => new IndexBuilder(),
            (root, state, localBuilder) =>
            {
                bool isDriveRoot = string.Equals(root, driveRoot, StringComparison.OrdinalIgnoreCase);
                var opts = isDriveRoot ? TopDirOpts : FileOpts; // root handled shallowly to avoid double-walking subdirs

                try
                {
                    foreach (string path in Directory.EnumerateFiles(root, "*", opts))
                    {
                        if (ct.IsCancellationRequested) break;
                        int sep = path.LastIndexOf('\\');
                        string dir = sep > 0 ? path.Substring(0, sep) : root;
                        ReadOnlySpan<char> name = path.AsSpan(sep + 1);
                        localBuilder.Add(dir, name);

                        long n = Interlocked.Increment(ref total);
                        if ((n & 0x3FFF) == 0) onProgress(n, dir); // throttle UI updates
                    }
                }
                catch { /* tolerate per-tree failures */ }

                return localBuilder;
            },
            localBuilder =>
            {
                lock (mergeLock)
                {
                    target.Merge(localBuilder);
                }
            });

        onProgress(total, driveRoot);
    }
}
