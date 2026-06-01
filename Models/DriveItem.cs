using FileFinder.ViewModels;

namespace FileFinder.Models;

public sealed class DriveItem : ObservableObject
{
    private bool _isSelected;

    public string Root { get; init; } = "";       // e.g. "C:\"
    public string Label { get; init; } = "";       // e.g. "C: (Windows) — NTFS, 250 GB free"
    public string Format { get; init; } = "";       // e.g. "NTFS"

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }
}
