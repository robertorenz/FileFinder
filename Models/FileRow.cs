using System.IO;

namespace FileFinder.Models;

/// <summary>A single result row shown in the grid (materialized lazily on search).</summary>
public sealed class FileRow
{
    public required string Name { get; init; }
    public required string Directory { get; init; }
    public required string FullPath { get; init; }
    public string Extension { get; init; } = "";

    // Metadata, populated lazily only when a metadata column is visible.
    public string SizeText { get; private set; } = "";
    public string ModifiedText { get; private set; } = "";
    public string AttributesText { get; private set; } = "";

    /// <summary>Reads size / modified / attributes from disk. Safe on missing files.</summary>
    public void LoadMetadata()
    {
        try
        {
            var fi = new FileInfo(FullPath);
            if (!fi.Exists) return;
            SizeText = HumanSize(fi.Length);
            ModifiedText = fi.LastWriteTime.ToString("g");
            AttributesText = FormatAttributes(fi.Attributes);
        }
        catch { /* file vanished or access denied — leave blank */ }
    }

    private static string FormatAttributes(FileAttributes a)
    {
        Span<char> buf = stackalloc char[4];
        int n = 0;
        if ((a & FileAttributes.ReadOnly) != 0) buf[n++] = 'R';
        if ((a & FileAttributes.Hidden) != 0) buf[n++] = 'H';
        if ((a & FileAttributes.System) != 0) buf[n++] = 'S';
        if ((a & FileAttributes.Archive) != 0) buf[n++] = 'A';
        return new string(buf[..n]);
    }

    private static string HumanSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes;
        int u = 0;
        while (v >= 1024 && u < units.Length - 1) { v /= 1024; u++; }
        return u == 0 ? $"{bytes} B" : $"{v:0.0} {units[u]}";
    }
}
