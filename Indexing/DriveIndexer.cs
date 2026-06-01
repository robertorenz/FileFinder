using System.IO;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using FileFinder.Core;

namespace FileFinder.Indexing;

/// <summary>
/// Top-level indexing orchestrator. For each selected drive it tries the fast
/// MFT path first (when elevated + NTFS) and transparently falls back to the
/// portable directory walk otherwise.
/// </summary>
public static class DriveIndexer
{
    /// <summary>True when the process is running with Administrator rights.</summary>
    public static bool IsElevated()
    {
        try
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    public static Task<FileIndex> BuildAsync(IReadOnlyList<string> drives,
        IProgress<IndexProgress> progress, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            var builder = new IndexBuilder();
            var mergeLock = new object();
            bool elevated = IsElevated();

            foreach (string drive in drives)
            {
                ct.ThrowIfCancellationRequested();
                string root = drive.EndsWith('\\') ? drive : drive + "\\";

                IndexMethod method = IndexMethod.DirectoryWalk;
                bool mftDone = false;

                if (elevated)
                {
                    void MftProgress(long n, string path) =>
                        progress.Report(new IndexProgress(drive, IndexMethod.Mft, n, path, false));

                    mftDone = MftReader.TryIndex(root, builder, MftProgress, ct);
                    if (mftDone) method = IndexMethod.Mft;
                }

                if (!mftDone)
                {
                    void WalkProgress(long n, string path) =>
                        progress.Report(new IndexProgress(drive, IndexMethod.DirectoryWalk, n, path, false));

                    DirectoryWalker.Index(root, builder, mergeLock, WalkProgress, ct);
                    method = IndexMethod.DirectoryWalk;
                }

                progress.Report(new IndexProgress(drive, method, builder.Count, root, false));
            }

            var index = builder.Build(drives.ToArray(), DateTime.UtcNow);
            progress.Report(new IndexProgress("", IndexMethod.None, index.Count, "", true));
            return index;
        }, ct);
    }
}
