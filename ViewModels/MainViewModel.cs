using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using FileFinder.Core;
using FileFinder.Dialogs;
using FileFinder.Indexing;
using FileFinder.Models;

namespace FileFinder.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private const int ResultLimit = 5000;

    private FileIndex? _index;
    private CancellationTokenSource? _indexCts;
    private CancellationTokenSource? _searchCts;

    public ObservableCollection<DriveItem> Drives { get; } = new();
    public ObservableCollection<FileRow> Results { get; } = new();

    public RelayCommand IndexCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand RestartAsAdminCommand { get; }
    public RelayCommand OpenFileCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand ClearIndexCommand { get; }

    public MainViewModel()
    {
        IndexCommand = new RelayCommand(_ => _ = BuildIndexAsync(), _ => !IsIndexing && AnyDriveSelected);
        CancelCommand = new RelayCommand(_ => _indexCts?.Cancel(), _ => IsIndexing);
        RestartAsAdminCommand = new RelayCommand(_ => RestartElevated(), _ => !IsElevated);
        OpenFileCommand = new RelayCommand(OpenFile);
        OpenFolderCommand = new RelayCommand(OpenFolder);
        ClearIndexCommand = new RelayCommand(_ => ClearIndex(), _ => _index != null && !IsIndexing);

        LoadDrives();
        _ = LoadCachedIndexAsync();
    }

    // ---------------- properties ----------------

    private string _query = "";
    public string Query
    {
        get => _query;
        set { if (SetField(ref _query, value)) ScheduleSearch(); }
    }

    private string _statusText = "Ready.";
    public string StatusText { get => _statusText; set => SetField(ref _statusText, value); }

    private string _resultCountText = "";
    public string ResultCountText { get => _resultCountText; set => SetField(ref _resultCountText, value); }

    private string _indexSummary = "No index yet — select drives and click Build Index.";
    public string IndexSummary { get => _indexSummary; set => SetField(ref _indexSummary, value); }

    private double _progressValue;
    public double ProgressValue { get => _progressValue; set => SetField(ref _progressValue, value); }

    private bool _isIndeterminate;
    public bool IsIndeterminate { get => _isIndeterminate; set => SetField(ref _isIndeterminate, value); }

    private bool _isIndexing;
    public bool IsIndexing
    {
        get => _isIndexing;
        set { if (SetField(ref _isIndexing, value)) RaiseCommands(); }
    }

    public bool IsElevated { get; } = DriveIndexer.IsElevated();

    public string AdminStatusText => IsElevated
        ? "Administrator — fast MFT indexing available"
        : "Standard user — using portable folder scan";

    private bool AnyDriveSelected => Drives.Any(d => d.IsSelected);

    // ---------------- drives ----------------

    private void LoadDrives()
    {
        foreach (var di in DriveInfo.GetDrives())
        {
            try
            {
                if (!di.IsReady) continue;
                if (di.DriveType is not (DriveType.Fixed or DriveType.Removable or DriveType.Network))
                    continue;

                string freeGb = (di.AvailableFreeSpace / 1024d / 1024 / 1024).ToString("0");
                string vol = string.IsNullOrWhiteSpace(di.VolumeLabel) ? "Local Disk" : di.VolumeLabel;
                Drives.Add(new DriveItem
                {
                    Root = di.RootDirectory.FullName,
                    Format = di.DriveFormat,
                    Label = $"{di.Name.TrimEnd('\\')}  ({vol})  ·  {di.DriveFormat}, {freeGb} GB free",
                    IsSelected = string.Equals(di.RootDirectory.FullName,
                        Path.GetPathRoot(Environment.SystemDirectory), StringComparison.OrdinalIgnoreCase)
                });

                Drives[^1].PropertyChanged += (_, __) => RaiseCommands();
            }
            catch { /* skip unreadable drive */ }
        }
    }

    // ---------------- indexing ----------------

    private async Task LoadCachedIndexAsync()
    {
        string path = IndexCache.IndexPath;
        if (!File.Exists(path)) return;

        StatusText = "Loading saved index…";
        IsIndeterminate = true;
        var idx = await Task.Run(() => FileIndex.TryLoad(path));
        IsIndeterminate = false;

        if (idx != null)
        {
            _index = idx;
            IndexSummary = $"Loaded {idx.Count:N0} files from cache " +
                           $"({string.Join(", ", idx.Drives.Select(d => d.TrimEnd('\\')))}) · " +
                           $"built {idx.BuiltUtc.ToLocalTime():g}";
            StatusText = "Index ready. Start typing to search.";
            RaiseCommands();
        }
        else
        {
            StatusText = "Ready.";
        }
    }

    private async Task BuildIndexAsync()
    {
        var selected = Drives.Where(d => d.IsSelected).Select(d => d.Root).ToList();
        if (selected.Count == 0)
        {
            ModalDialog.Show(Application.Current.MainWindow, "No drives selected",
                "Select at least one drive to index.");
            return;
        }

        _indexCts = new CancellationTokenSource();
        IsIndexing = true;
        IsIndeterminate = true;
        ProgressValue = 0;
        Results.Clear();
        ResultCountText = "";

        var sw = Stopwatch.StartNew();
        var progress = new Progress<IndexProgress>(p =>
        {
            if (p.Done) return;
            string method = p.Method == IndexMethod.Mft ? "MFT (fast)" : "Scanning";
            StatusText = $"{method} · {p.Drive.TrimEnd('\\')} · {p.FilesSoFar:N0} files · {Shorten(p.CurrentPath)}";
        });

        try
        {
            _index = await DriveIndexer.BuildAsync(selected, progress, _indexCts.Token);
            sw.Stop();

            await Task.Run(() => _index!.Save(IndexCache.IndexPath));

            IndexSummary = $"{_index.Count:N0} files indexed across " +
                           $"{string.Join(", ", selected.Select(d => d.TrimEnd('\\')))} " +
                           $"in {sw.Elapsed.TotalSeconds:0.0}s";
            StatusText = "Index ready. Start typing to search.";

            if (!string.IsNullOrEmpty(Query)) ScheduleSearch();
        }
        catch (OperationCanceledException)
        {
            StatusText = "Indexing cancelled.";
            IndexSummary = "Indexing cancelled — no index built.";
        }
        catch (Exception ex)
        {
            ModalDialog.Show(Application.Current.MainWindow, "Indexing failed", ex.Message);
            StatusText = "Indexing failed.";
        }
        finally
        {
            IsIndexing = false;
            IsIndeterminate = false;
            ProgressValue = 0;
            RaiseCommands();
        }
    }

    private void ClearIndex()
    {
        if (!ModalDialog.Confirm(Application.Current.MainWindow, "Clear index",
                "Remove the in-memory index and delete the saved cache file?", "Clear"))
            return;

        _index = null;
        Results.Clear();
        ResultCountText = "";
        try { if (File.Exists(IndexCache.IndexPath)) File.Delete(IndexCache.IndexPath); } catch { }
        IndexSummary = "No index — select drives and click Build Index.";
        StatusText = "Index cleared.";
        RaiseCommands();
    }

    // ---------------- search ----------------

    private void ScheduleSearch()
    {
        _searchCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchCts = cts;
        _ = RunSearchAsync(Query, cts.Token);
    }

    private async Task RunSearchAsync(string query, CancellationToken ct)
    {
        try { await Task.Delay(110, ct); } catch { return; } // debounce keystrokes

        var index = _index;
        if (index == null)
        {
            Results.Clear();
            ResultCountText = _query.Length > 0 ? "Build an index first." : "";
            return;
        }
        if (string.IsNullOrEmpty(query))
        {
            Results.Clear();
            ResultCountText = "";
            return;
        }

        var sw = Stopwatch.StartNew();
        var (hits, total) = await Task.Run(() => index.Search(query, ResultLimit), ct);
        if (ct.IsCancellationRequested) return;

        var rows = new List<FileRow>(hits.Count);
        foreach (int i in hits)
        {
            string name = index.GetName(i);
            rows.Add(new FileRow
            {
                Name = name,
                Directory = index.GetDirectory(i),
                FullPath = index.GetFullPath(i),
                Extension = Path.GetExtension(name)
            });
        }
        sw.Stop();

        if (ct.IsCancellationRequested) return;
        Results.Clear();
        foreach (var r in rows) Results.Add(r);

        ResultCountText = total > ResultLimit
            ? $"{total:N0} matches (showing first {ResultLimit:N0}) · {sw.Elapsed.TotalMilliseconds:0} ms"
            : $"{total:N0} match{(total == 1 ? "" : "es")} · {sw.Elapsed.TotalMilliseconds:0} ms";
    }

    // ---------------- file actions ----------------

    private void OpenFile(object? param)
    {
        if (param is not FileRow row) return;
        try { Process.Start(new ProcessStartInfo(row.FullPath) { UseShellExecute = true }); }
        catch (Exception ex)
        {
            ModalDialog.Show(Application.Current.MainWindow, "Cannot open file", ex.Message);
        }
    }

    private void OpenFolder(object? param)
    {
        if (param is not FileRow row) return;
        try
        {
            if (File.Exists(row.FullPath))
                Process.Start("explorer.exe", $"/select,\"{row.FullPath}\"");
            else
                Process.Start(new ProcessStartInfo(row.Directory) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ModalDialog.Show(Application.Current.MainWindow, "Cannot open folder", ex.Message);
        }
    }

    private void RestartElevated()
    {
        try
        {
            string exe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule!.FileName!;
            var psi = new ProcessStartInfo(exe) { UseShellExecute = true, Verb = "runas" };
            Process.Start(psi);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            // User declined the UAC prompt, or relaunch failed.
            ModalDialog.Show(Application.Current.MainWindow, "Could not restart as Administrator", ex.Message);
        }
    }

    // ---------------- helpers ----------------

    private void RaiseCommands()
    {
        IndexCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
        ClearIndexCommand.RaiseCanExecuteChanged();
        RestartAsAdminCommand.RaiseCanExecuteChanged();
    }

    private static string Shorten(string path, int max = 48)
    {
        if (string.IsNullOrEmpty(path) || path.Length <= max) return path;
        return "…" + path[^max..];
    }
}
