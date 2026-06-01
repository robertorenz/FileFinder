using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace FileFinder.Core;

/// <summary>
/// Immutable, search-optimized snapshot of every indexed file.
///
/// Memory layout is deliberately flat so we can hold millions of records without
/// drowning the GC in tiny objects:
///   * Directories are de-duplicated into <see cref="_dirs"/>; each file keeps a
///     small int id into that table.
///   * Original-case file names live in one big char blob (_nameChars) addressed
///     by offsets — used only to render the rows the user actually sees.
///   * Lower-cased UTF-8 file names live in one big byte blob (_lowerBytes), the
///     buffer the SIMD matcher scans. It is padded with 32 trailing bytes so the
///     vector loads never read past the array.
/// </summary>
public sealed class FileIndex
{
    public const int SlackBytes = 32;

    private readonly string[] _dirs;
    private readonly int[] _dirIds;       // length = Count
    private readonly char[] _nameChars;   // original-case name blob
    private readonly int[] _nameOff;      // length = Count + 1
    private readonly byte[] _lowerBytes;  // lower-cased utf8 name blob (+slack)
    private readonly int[] _lowerOff;     // length = Count + 1

    public int Count { get; }
    public string[] Drives { get; }
    public DateTime BuiltUtc { get; }

    internal FileIndex(string[] dirs, int[] dirIds, char[] nameChars, int[] nameOff,
                       byte[] lowerBytes, int[] lowerOff, string[] drives, DateTime builtUtc)
    {
        _dirs = dirs;
        _dirIds = dirIds;
        _nameChars = nameChars;
        _nameOff = nameOff;
        _lowerBytes = lowerBytes;
        _lowerOff = lowerOff;
        Drives = drives;
        BuiltUtc = builtUtc;
        Count = dirIds.Length;
    }

    public string GetName(int i) => new string(_nameChars, _nameOff[i], _nameOff[i + 1] - _nameOff[i]);

    public string GetDirectory(int i) => _dirs[_dirIds[i]];

    public string GetFullPath(int i)
    {
        string dir = _dirs[_dirIds[i]];
        string name = GetName(i);
        return dir.EndsWith('\\') ? dir + name : dir + "\\" + name;
    }

    /// <summary>Number of unique directories referenced by the index.</summary>
    public int DirectoryCount => _dirs.Length;

    /// <summary>
    /// Approximate RAM held by the index: the flat blobs plus the directory
    /// string table. Good enough to show the user the order of magnitude.
    /// </summary>
    public long ApproxMemoryBytes
    {
        get
        {
            long bytes = (long)_dirIds.Length * sizeof(int)
                       + (long)_nameOff.Length * sizeof(int)
                       + (long)_lowerOff.Length * sizeof(int)
                       + (long)_nameChars.Length * sizeof(char)
                       + _lowerBytes.Length;
            foreach (string d in _dirs) bytes += 24 + (long)d.Length * sizeof(char); // obj overhead + chars
            return bytes;
        }
    }

    /// <summary>
    /// Tallies file extensions across the whole index and returns the most
    /// common ones. Files without an extension are grouped as "(no extension)".
    /// </summary>
    public List<(string Ext, int Count)> TopExtensions(int top)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < Count; i++)
        {
            int s = _nameOff[i], e = _nameOff[i + 1];
            int dot = -1;
            for (int j = e - 1; j >= s; j--)
            {
                char c = _nameChars[j];
                if (c == '.') { dot = j; break; }
                if (c == '\\' || c == '/') break;
            }
            string ext = (dot >= 0 && dot < e - 1)
                ? new string(_nameChars, dot, e - dot).ToLowerInvariant()
                : "(no extension)";
            map.TryGetValue(ext, out int cnt);
            map[ext] = cnt + 1;
        }
        var list = new List<(string, int)>(map.Count);
        foreach (var kv in map) list.Add((kv.Key, kv.Value));
        list.Sort((a, b) => b.Item2.CompareTo(a.Item2));
        if (list.Count > top) list.RemoveRange(top, list.Count - top);
        return list;
    }

    /// <summary>
    /// Runs the SIMD substring search across every name in parallel and returns
    /// the matching record indices (capped at <paramref name="limit"/>) plus the
    /// true total number of matches.
    /// </summary>
    public unsafe (List<int> hits, int total) Search(string query, int limit)
    {
        if (string.IsNullOrEmpty(query))
            return (new List<int>(), 0);

        byte[] needle = Encoding.UTF8.GetBytes(query.ToLowerInvariant());
        bool glob = WildcardMatcher.HasWildcard(query);

        int partitions = Math.Max(1, Environment.ProcessorCount);
        var local = new List<int>[partitions];
        var counts = new int[partitions];
        int n = Count;

        Parallel.For(0, partitions, pIdx =>
        {
            int from = (int)((long)n * pIdx / partitions);
            int to = (int)((long)n * (pIdx + 1) / partitions);
            var list = new List<int>();

            fixed (byte* blob = _lowerBytes)
            fixed (byte* nd = needle)
            fixed (int* off = _lowerOff)
            {
                for (int i = from; i < to; i++)
                {
                    int start = off[i];
                    int len = off[i + 1] - start;
                    bool hit = glob
                        ? WildcardMatcher.Match(blob + start, len, nd, needle.Length)
                        : SimdSearch.Contains(blob, start, len, nd, needle.Length);
                    if (hit)
                    {
                        counts[pIdx]++;
                        if (list.Count < limit)
                            list.Add(i);
                    }
                }
            }
            local[pIdx] = list;
        });

        int total = 0;
        for (int p = 0; p < partitions; p++) total += counts[p];

        var hits = new List<int>(Math.Min(limit, total));
        for (int p = 0; p < partitions && hits.Count < limit; p++)
        {
            foreach (int idx in local[p])
            {
                hits.Add(idx);
                if (hits.Count >= limit) break;
            }
        }
        return (hits, total);
    }

    // ---- binary cache format (v1) ----
    private const int Magic = 0x46464958; // "FFIX"
    private const int Version = 1;

    public void Save(string path)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20);
        using var w = new BinaryWriter(fs, Encoding.UTF8);
        w.Write(Magic);
        w.Write(Version);
        w.Write(BuiltUtc.ToBinary());

        w.Write(Drives.Length);
        foreach (var d in Drives) w.Write(d);

        w.Write(_dirs.Length);
        foreach (var d in _dirs) w.Write(d);

        w.Write(Count);
        WriteInts(w, _dirIds);
        WriteInts(w, _nameOff);
        w.Write(_nameChars.Length);
        foreach (char c in _nameChars) w.Write((short)c);
        WriteInts(w, _lowerOff);
        w.Write(_lowerBytes.Length);
        w.Write(_lowerBytes);
    }

    public static FileIndex? TryLoad(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20);
            using var r = new BinaryReader(fs, Encoding.UTF8);
            if (r.ReadInt32() != Magic) return null;
            if (r.ReadInt32() != Version) return null;
            var built = DateTime.FromBinary(r.ReadInt64());

            var drives = new string[r.ReadInt32()];
            for (int i = 0; i < drives.Length; i++) drives[i] = r.ReadString();

            var dirs = new string[r.ReadInt32()];
            for (int i = 0; i < dirs.Length; i++) dirs[i] = r.ReadString();

            int count = r.ReadInt32();
            int[] dirIds = ReadInts(r, count);
            int[] nameOff = ReadInts(r, count + 1);
            int nameCharLen = r.ReadInt32();
            var nameChars = new char[nameCharLen];
            for (int i = 0; i < nameCharLen; i++) nameChars[i] = (char)r.ReadInt16();
            int[] lowerOff = ReadInts(r, count + 1);
            int lowerLen = r.ReadInt32();
            byte[] lowerBytes = r.ReadBytes(lowerLen);

            return new FileIndex(dirs, dirIds, nameChars, nameOff, lowerBytes, lowerOff, drives, built);
        }
        catch
        {
            return null;
        }
    }

    private static void WriteInts(BinaryWriter w, int[] a)
    {
        w.Write(a.Length);
        var bytes = new byte[a.Length * sizeof(int)];
        Buffer.BlockCopy(a, 0, bytes, 0, bytes.Length);
        w.Write(bytes);
    }

    private static int[] ReadInts(BinaryReader r, int expected)
    {
        int len = r.ReadInt32();
        var a = new int[len];
        var bytes = r.ReadBytes(len * sizeof(int));
        Buffer.BlockCopy(bytes, 0, a, 0, bytes.Length);
        return a;
    }
}
