using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using FileFinder.Core;

namespace FileFinder.Indexing;

/// <summary>
/// Fast NTFS indexer. Opens the raw volume and enumerates the Master File Table
/// through FSCTL_ENUM_USN_DATA — the same technique "Everything" uses to index a
/// whole drive in seconds. Every record yields its own file-reference-number, its
/// parent's, and its name; we stitch those into full paths afterwards.
///
/// Requires Administrator rights and an NTFS volume. <see cref="TryIndex"/>
/// returns false (without throwing) when either prerequisite is missing so the
/// caller can fall back to the directory walk.
/// </summary>
public static class MftReader
{
    private const uint GENERIC_READ = 0x80000000;
    private const uint FILE_SHARE_READ = 0x1;
    private const uint FILE_SHARE_WRITE = 0x2;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;

    private const uint FSCTL_ENUM_USN_DATA = 0x000900b3;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
    private const ulong RootFrn = 0x0005000000000005; // NTFS root directory reference

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes,
        uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice, uint dwIoControlCode,
        ref MFT_ENUM_DATA lpInBuffer, int nInBufferSize,
        IntPtr lpOutBuffer, int nOutBufferSize,
        out int lpBytesReturned, IntPtr lpOverlapped);

    [StructLayout(LayoutKind.Sequential)]
    private struct MFT_ENUM_DATA
    {
        public ulong StartFileReferenceNumber;
        public long LowUsn;
        public long HighUsn;
    }

    private readonly struct Entry
    {
        public readonly ulong Parent;
        public readonly string Name;
        public readonly bool IsDir;
        public Entry(ulong parent, string name, bool isDir) { Parent = parent; Name = name; IsDir = isDir; }
    }

    public static bool TryIndex(string driveRoot, IndexBuilder target,
                                Action<long, string> onProgress, CancellationToken ct)
    {
        // driveRoot like "C:\" -> volume path "\\.\C:"
        char letter = char.ToUpperInvariant(driveRoot[0]);
        if (!IsNtfs(driveRoot)) return false;

        string volumePath = $@"\\.\{letter}:";
        using SafeFileHandle h = CreateFileW(volumePath, GENERIC_READ,
            FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING,
            FILE_FLAG_BACKUP_SEMANTICS, IntPtr.Zero);

        if (h.IsInvalid) return false; // not elevated / no access

        try
        {
            var entries = EnumerateRecords(h, ct, onProgress);
            if (entries.Count == 0) return false;
            ResolveAndEmit(entries, $"{letter}:", target, onProgress, ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Dictionary<ulong, Entry> EnumerateRecords(
        SafeFileHandle h, CancellationToken ct, Action<long, string> onProgress)
    {
        var map = new Dictionary<ulong, Entry>(1 << 20);
        const int bufSize = 1 << 16; // 64 KB
        IntPtr buffer = Marshal.AllocHGlobal(bufSize);
        try
        {
            var med = new MFT_ENUM_DATA { StartFileReferenceNumber = 0, LowUsn = 0, HighUsn = long.MaxValue };
            long seen = 0;

            while (!ct.IsCancellationRequested &&
                   DeviceIoControl(h, FSCTL_ENUM_USN_DATA, ref med, Marshal.SizeOf<MFT_ENUM_DATA>(),
                                   buffer, bufSize, out int bytesReturned, IntPtr.Zero))
            {
                if (bytesReturned <= 8) break; // only the next-FRN cursor came back

                // First 8 bytes = next start reference number.
                med.StartFileReferenceNumber = (ulong)Marshal.ReadInt64(buffer);

                int offset = 8;
                while (offset < bytesReturned)
                {
                    IntPtr rec = buffer + offset;
                    int recordLength = Marshal.ReadInt32(rec, 0);
                    if (recordLength <= 0) break;

                    ulong frn = (ulong)Marshal.ReadInt64(rec, 8);
                    ulong parent = (ulong)Marshal.ReadInt64(rec, 16);
                    uint attrs = (uint)Marshal.ReadInt32(rec, 52);
                    short nameLength = Marshal.ReadInt16(rec, 56);
                    short nameOffset = Marshal.ReadInt16(rec, 58);

                    string name = Marshal.PtrToStringUni(rec + nameOffset, nameLength / 2);
                    bool isDir = (attrs & FILE_ATTRIBUTE_DIRECTORY) != 0;
                    map[frn] = new Entry(parent, name, isDir);

                    offset += recordLength;
                    if ((++seen & 0x1FFFF) == 0) onProgress(seen, name);
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
        return map;
    }

    private static void ResolveAndEmit(Dictionary<ulong, Entry> map, string driveLabel,
                                       IndexBuilder target, Action<long, string> onProgress, CancellationToken ct)
    {
        var dirCache = new Dictionary<ulong, string>(map.Count / 4);
        var stack = new List<ulong>(64);
        long emitted = 0;

        foreach (var kv in map)
        {
            if (ct.IsCancellationRequested) break;
            Entry e = kv.Value;
            if (e.IsDir) continue; // only emit actual files

            string dir = ResolveDir(e.Parent, map, dirCache, stack, driveLabel);
            if (dir is null) continue;

            target.Add(dir, e.Name.AsSpan());
            if ((++emitted & 0x1FFFF) == 0) onProgress(emitted, dir);
        }
        onProgress(emitted, driveLabel);
    }

    private static string ResolveDir(ulong frn, Dictionary<ulong, Entry> map,
                                     Dictionary<ulong, string> cache, List<ulong> stack, string driveLabel)
    {
        if (frn == RootFrn) return driveLabel;
        if (cache.TryGetValue(frn, out string? cached)) return cached;

        stack.Clear();
        ulong cur = frn;
        // Walk up to the root (or an already-cached ancestor), guarding against
        // orphaned records and cycles.
        while (cur != RootFrn && !cache.ContainsKey(cur))
        {
            if (!map.TryGetValue(cur, out Entry e)) return null!; // orphaned
            if (!e.IsDir) return null!;
            stack.Add(cur);
            cur = e.Parent;
            if (stack.Count > 4096) return null!; // pathological cycle guard
        }

        string path = cur == RootFrn ? driveLabel : cache[cur];
        for (int i = stack.Count - 1; i >= 0; i--)
        {
            ulong id = stack[i];
            path = path + "\\" + map[id].Name;
            cache[id] = path;
        }
        return path;
    }

    private static bool IsNtfs(string driveRoot)
    {
        try
        {
            var di = new DriveInfo(driveRoot);
            return di.IsReady && string.Equals(di.DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}
