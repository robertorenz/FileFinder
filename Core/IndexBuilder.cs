using System.Text;

namespace FileFinder.Core;

/// <summary>
/// Accumulates files into the flat blobs that back <see cref="FileIndex"/>.
/// Not thread-safe on its own: each worker fills its own builder, then we
/// <see cref="Merge"/> them under a lock at the end of indexing.
/// </summary>
public sealed class IndexBuilder
{
    private readonly Dictionary<string, int> _dirMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _dirs = new();

    private int[] _dirIds = new int[1024];
    private int _count;

    private char[] _nameChars = new char[1 << 16];
    private int _nameLen;
    private int[] _nameOff = new int[1025];

    private byte[] _lowerBytes = new byte[1 << 16];
    private int _lowerLen;
    private int[] _lowerOff = new int[1025];

    // Reusable scratch buffer for lower-casing before UTF-8 encoding.
    private char[] _scratch = new char[512];

    public int Count => _count;

    public void Add(string directory, ReadOnlySpan<char> name)
    {
        if (!_dirMap.TryGetValue(directory, out int dirId))
        {
            dirId = _dirs.Count;
            _dirs.Add(directory);
            _dirMap[directory] = dirId;
        }

        EnsureCount();
        _nameOff[_count] = _nameLen;
        _lowerOff[_count] = _lowerLen;
        _dirIds[_count] = dirId;

        // original-case name
        EnsureChars(name.Length);
        name.CopyTo(_nameChars.AsSpan(_nameLen));
        _nameLen += name.Length;

        // lower-cased utf8 name
        if (_scratch.Length < name.Length) _scratch = new char[name.Length];
        for (int i = 0; i < name.Length; i++) _scratch[i] = char.ToLowerInvariant(name[i]);
        EnsureBytes(name.Length * 3);
        int written = Encoding.UTF8.GetBytes(_scratch.AsSpan(0, name.Length), _lowerBytes.AsSpan(_lowerLen));
        _lowerLen += written;

        _count++;

        // Keep the terminator slot live after every Add so Merge can safely read
        // _nameOff[_count] / _lowerOff[_count] for the last element. EnsureCount
        // reserves room for this extra slot (+1). Build() re-writes it harmlessly.
        _nameOff[_count] = _nameLen;
        _lowerOff[_count] = _lowerLen;
    }

    /// <summary>Appends another builder's contents into this one, remapping dir ids.</summary>
    public void Merge(IndexBuilder other)
    {
        var remap = new int[other._dirs.Count];
        for (int i = 0; i < other._dirs.Count; i++)
        {
            string dir = other._dirs[i];
            if (!_dirMap.TryGetValue(dir, out int id))
            {
                id = _dirs.Count;
                _dirs.Add(dir);
                _dirMap[dir] = id;
            }
            remap[i] = id;
        }

        EnsureCount(other._count);
        EnsureChars(other._nameLen);
        EnsureBytes(other._lowerLen);

        for (int i = 0; i < other._count; i++)
        {
            _nameOff[_count] = _nameLen;
            _lowerOff[_count] = _lowerLen;
            _dirIds[_count] = remap[other._dirIds[i]];

            int nStart = other._nameOff[i];
            int nLen = other._nameOff[i + 1] - nStart;
            Array.Copy(other._nameChars, nStart, _nameChars, _nameLen, nLen);
            _nameLen += nLen;

            int lStart = other._lowerOff[i];
            int lLen = other._lowerOff[i + 1] - lStart;
            Array.Copy(other._lowerBytes, lStart, _lowerBytes, _lowerLen, lLen);
            _lowerLen += lLen;

            _count++;
        }

        // Maintain the terminator slot (see Add) so this builder stays mergeable.
        _nameOff[_count] = _nameLen;
        _lowerOff[_count] = _lowerLen;
    }

    public FileIndex Build(string[] drives, DateTime builtUtc)
    {
        _nameOff[_count] = _nameLen;
        _lowerOff[_count] = _lowerLen;

        var dirIds = new int[_count];
        Array.Copy(_dirIds, dirIds, _count);
        var nameOff = new int[_count + 1];
        Array.Copy(_nameOff, nameOff, _count + 1);
        var lowerOff = new int[_count + 1];
        Array.Copy(_lowerOff, lowerOff, _count + 1);

        var nameChars = new char[_nameLen];
        Array.Copy(_nameChars, nameChars, _nameLen);

        // Pad the search blob with slack so SIMD loads never read out of bounds.
        var lowerBytes = new byte[_lowerLen + FileIndex.SlackBytes];
        Array.Copy(_lowerBytes, lowerBytes, _lowerLen);

        return new FileIndex(_dirs.ToArray(), dirIds, nameChars, nameOff, lowerBytes, lowerOff, drives, builtUtc);
    }

    private void EnsureCount(int extra = 1)
    {
        if (_count + extra + 1 > _dirIds.Length)
        {
            int target = Math.Max(_dirIds.Length * 2, _count + extra + 1);
            Array.Resize(ref _dirIds, target);
            Array.Resize(ref _nameOff, target + 1);
            Array.Resize(ref _lowerOff, target + 1);
        }
    }

    private void EnsureChars(int extra)
    {
        if (_nameLen + extra > _nameChars.Length)
            Array.Resize(ref _nameChars, Math.Max(_nameChars.Length * 2, _nameLen + extra));
    }

    private void EnsureBytes(int extra)
    {
        if (_lowerLen + extra > _lowerBytes.Length)
            Array.Resize(ref _lowerBytes, Math.Max(_lowerBytes.Length * 2, _lowerLen + extra));
    }
}
