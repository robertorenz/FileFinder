namespace FileFinder.Models;

/// <summary>A single result row shown in the grid (materialized lazily on search).</summary>
public sealed class FileRow
{
    public required string Name { get; init; }
    public required string Directory { get; init; }
    public required string FullPath { get; init; }
    public string Extension { get; init; } = "";
}
