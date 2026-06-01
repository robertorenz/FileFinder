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
using Loc = FileFinder.Localization.Localization;

namespace FileFinder.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private const int ResultLimit = 5000;

    private FileIndex? _index;
    private CancellationTokenSource? _indexCts;
    private CancellationTokenSource? _searchCts;
    private readonly AppSettings _settings;

    private static string L(string key) => Loc.Instance[key];
    private static string LF(string key, params object[] args) => Loc.Instance.Format(key, args);

    public ObservableCollection<DriveItem> Drives { get; } = new();
    public ObservableCollection<FileRow> Results { get; } = new();

    public RelayCommand IndexCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand RestartAsAdminCommand { get; }
    public RelayCommand OpenFileCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand OpenWithCommand { get; }
    public RelayCommand CopyFileCommand { get; }
    public RelayCommand CopyFullPathCommand { get; }
    public RelayCommand CopyFolderPathCommand { get; }
    public RelayCommand CopyFileNameCommand { get; }
    public RelayCommand CopyNameNoExtCommand { get; }
    public RelayCommand CopySizeCommand { get; }
    public RelayCommand CopyDateCommand { get; }
    public RelayCommand RunAsAdminCommand { get; }
    public RelayCommand OpenInTerminalCommand { get; }
    public RelayCommand FindByTypeCommand { get; }
    public RelayCommand PropertiesCommand { get; }
    public RelayCommand ClearIndexCommand { get; }
    public RelayCommand ShowStatisticsCommand { get; }
    public RelayCommand OpenCacheFolderCommand { get; }
    public RelayCommand AboutCommand { get; }
    public RelayCommand BenchmarkCommand { get; }
    public RelayCommand PreferencesCommand { get; }
    public RelayCommand DocumentationCommand { get; }

    private const string DocsUrl = "https://github.com/robertorenz/FileFinder/blob/main/DOCS.md";

    public MainViewModel()
    {
        _settings = AppSettings.Load();
        Loc.Instance.CurrentLanguage = _settings.Language;
        _engine = (_settings.DefaultEngine == SearchEngine.Masm && MasmAvailable)
            ? SearchEngine.Masm : SearchEngine.Jit;

        IndexCommand = new RelayCommand(_ => _ = BuildIndexAsync(), _ => !IsIndexing && AnyDriveSelected);
        CancelCommand = new RelayCommand(_ => _indexCts?.Cancel(), _ => IsIndexing);
        RestartAsAdminCommand = new RelayCommand(_ => RestartElevated(), _ => !IsElevated);
        OpenFileCommand = new RelayCommand(OpenFile);
        OpenFolderCommand = new RelayCommand(OpenFolder);
        OpenWithCommand = new RelayCommand(OpenWith);
        CopyFileCommand = new RelayCommand(CopyFileToClipboard);
        CopyFullPathCommand = new RelayCommand(p => CopyText((p as FileRow)?.FullPath));
        CopyFolderPathCommand = new RelayCommand(p => CopyText((p as FileRow)?.Directory));
        CopyFileNameCommand = new RelayCommand(p => CopyText((p as FileRow)?.Name));
        CopyNameNoExtCommand = new RelayCommand(p =>
            CopyText(p is FileRow r ? Path.GetFileNameWithoutExtension(r.Name) : null));
        CopySizeCommand = new RelayCommand(p => { if (p is FileRow r) { r.LoadMetadata(); CopyText(r.SizeText); } });
        CopyDateCommand = new RelayCommand(p => { if (p is FileRow r) { r.LoadMetadata(); CopyText(r.ModifiedText); } });
        RunAsAdminCommand = new RelayCommand(RunAsAdmin);
        OpenInTerminalCommand = new RelayCommand(OpenInTerminal);
        FindByTypeCommand = new RelayCommand(FindByType);
        PropertiesCommand = new RelayCommand(ShowProperties);
        ClearIndexCommand = new RelayCommand(_ => ClearIndex(), _ => _index != null && !IsIndexing);
        ShowStatisticsCommand = new RelayCommand(_ =>
            StatisticsDialog.Show(Application.Current.MainWindow, _index, IndexCache.IndexPath));
        OpenCacheFolderCommand = new RelayCommand(_ => OpenCacheFolder());
        AboutCommand = new RelayCommand(_ => ShowAbout());
        BenchmarkCommand = new RelayCommand(_ => _ = RunBenchmarkAsync(), _ => !IsIndexing);
        PreferencesCommand = new RelayCommand(_ => ShowPreferences());
        DocumentationCommand = new RelayCommand(_ => OpenUrl(DocsUrl));

        _statusText = L("Ready");
        _indexSummary = L("NoIndexYet");

        // Re-localize the persistent computed/idle strings when the language changes.
        Loc.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Loc.CurrentLanguage)) OnLanguageChanged();
        };

        LoadDrives();
    }

    /// <summary>
    /// Called once the main window is shown: loads any cached index, and if there
    /// is none, opens Preferences so the user can pick drives and build one.
    /// </summary>
    public async Task InitializeAsync()
    {
        await LoadCachedIndexAsync();
        if (_index == null && !IsIndexing)
            ShowPreferences();
    }

    private void ShowPreferences()
    {
        var engine = _engine;
        string lang = _settings.Language;
        var result = PreferencesDialog.Show(Application.Current.MainWindow, ref engine, MasmAvailable,
            ref lang, Drives, IsElevated, IsElevated ? null : RestartElevated, ClearIndex, _settings);

        if (result == PrefResult.Cancel) return;

        _settings.DefaultEngine = engine;
        _settings.Language = lang;
        _settings.IndexedDrives = Drives.Where(d => d.IsSelected).Select(d => d.Root).ToList();
        _settings.Save();

        Loc.Instance.CurrentLanguage = lang;
        if (engine != _engine)
        {
            _engine = engine;
            OnPropertyChanged(nameof(EngineIsJit));
            OnPropertyChanged(nameof(EngineIsMasm));
        }
        RefreshColumns();
        RaiseCommands();

        switch (result)
        {
            case PrefResult.Build: _ = BuildIndexAsync(); break;
            case PrefResult.Benchmark: _ = RunBenchmarkAsync(); break;
            default: ScheduleSearch(); break; // refresh listing/metadata (incl. empty query)
        }
    }

    /// <summary>The currently active window (e.g. the open Preferences dialog), for dialog ownership.</summary>
    private static Window ActiveOwner() =>
        Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
        ?? Application.Current.MainWindow;

    private void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(AdminStatusText));
        OnPropertyChanged(nameof(EngineStatusText));
        // Refresh idle status/summary text in the new language.
        if (!IsIndexing)
        {
            StatusText = _index == null ? L("Ready") : L("IndexReady");
            if (_index == null) IndexSummary = L("NoIndexYet");
        }
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

    // ---- search engine selection ----
    public bool MasmAvailable { get; } = NativeSearch.IsAvailable;

    public string EngineStatusText => MasmAvailable ? L("EngineReady") : L("EngineUnavailable");

    private SearchEngine _engine = SearchEngine.Jit;

    public bool EngineIsJit
    {
        get => _engine == SearchEngine.Jit;
        set { if (value && _engine != SearchEngine.Jit) { _engine = SearchEngine.Jit; OnEngineChanged(); } }
    }

    public bool EngineIsMasm
    {
        get => _engine == SearchEngine.Masm;
        set { if (value && _engine != SearchEngine.Masm) { _engine = SearchEngine.Masm; OnEngineChanged(); } }
    }

    private void OnEngineChanged()
    {
        OnPropertyChanged(nameof(EngineIsJit));
        OnPropertyChanged(nameof(EngineIsMasm));
        // Persist the sidebar choice as the new default.
        _settings.DefaultEngine = _engine;
        _settings.Save();
        if (!string.IsNullOrEmpty(Query)) ScheduleSearch();
    }

    public string AdminStatusText => IsElevated ? L("AdminElevated") : L("AdminStandard");

    // ---- result column visibility (bound by DataGrid columns via BindingProxy) ----
    public bool ShowFolderColumn => _settings.ShowFolder;
    public bool ShowTypeColumn => _settings.ShowType;
    public bool ShowSizeColumn => _settings.ShowSize;
    public bool ShowModifiedColumn => _settings.ShowModified;
    public bool ShowAttributesColumn => _settings.ShowAttributes;

    private void RefreshColumns()
    {
        OnPropertyChanged(nameof(ShowFolderColumn));
        OnPropertyChanged(nameof(ShowTypeColumn));
        OnPropertyChanged(nameof(ShowSizeColumn));
        OnPropertyChanged(nameof(ShowModifiedColumn));
        OnPropertyChanged(nameof(ShowAttributesColumn));
    }

    private bool AnyDriveSelected => Drives.Any(d => d.IsSelected);

    // ---------------- drives ----------------

    private void LoadDrives()
    {
        var saved = _settings.IndexedDrives;
        string? systemRoot = Path.GetPathRoot(Environment.SystemDirectory);

        foreach (var di in DriveInfo.GetDrives())
        {
            try
            {
                if (!di.IsReady) continue;
                if (di.DriveType is not (DriveType.Fixed or DriveType.Removable or DriveType.Network))
                    continue;

                string root = di.RootDirectory.FullName;
                bool selected = saved.Count > 0
                    ? saved.Contains(root)
                    : string.Equals(root, systemRoot, StringComparison.OrdinalIgnoreCase);

                string freeGb = (di.AvailableFreeSpace / 1024d / 1024 / 1024).ToString("0");
                string vol = string.IsNullOrWhiteSpace(di.VolumeLabel) ? "Local Disk" : di.VolumeLabel;
                Drives.Add(new DriveItem
                {
                    Root = root,
                    Format = di.DriveFormat,
                    Label = $"{di.Name.TrimEnd('\\')}  ({vol})  ·  {di.DriveFormat}, {freeGb} GB free",
                    IsSelected = selected
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

        StatusText = L("LoadingIndex");
        IsIndeterminate = true;
        var idx = await Task.Run(() => FileIndex.TryLoad(path));
        IsIndeterminate = false;

        if (idx != null)
        {
            _index = idx;
            string drives = string.Join(", ", idx.Drives.Select(d => d.TrimEnd('\\')));
            IndexSummary = LF("LoadedFromCache", idx.Count.ToString("N0"), drives,
                idx.BuiltUtc.ToLocalTime().ToString("g"));
            StatusText = L("IndexReady");
            RaiseCommands();
            ScheduleSearch(); // show all files immediately (empty query)
        }
        else
        {
            StatusText = L("Ready");
        }
    }

    private async Task BuildIndexAsync()
    {
        var selected = Drives.Where(d => d.IsSelected).Select(d => d.Root).ToList();
        if (selected.Count == 0)
        {
            ModalDialog.Show(Application.Current.MainWindow, L("NoDrivesTitle"), L("NoDrivesMsg"));
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
            string method = p.Method == IndexMethod.Mft ? L("MethodMftFast") : L("MethodScanning");
            StatusText = LF("StatusIndexing", method, p.Drive.TrimEnd('\\'),
                p.FilesSoFar.ToString("N0"), Shorten(p.CurrentPath));
        });

        try
        {
            _index = await DriveIndexer.BuildAsync(selected, progress, _indexCts.Token);
            sw.Stop();

            await Task.Run(() => _index!.Save(IndexCache.IndexPath));

            string drives = string.Join(", ", selected.Select(d => d.TrimEnd('\\')));
            IndexSummary = LF("IndexedSummary", _index.Count.ToString("N0"), drives,
                sw.Elapsed.TotalSeconds.ToString("0.0"));
            StatusText = L("IndexReady");
            ScheduleSearch(); // show all files (or current query) once indexed
        }
        catch (OperationCanceledException)
        {
            StatusText = L("IndexingCancelled");
            IndexSummary = L("IndexingCancelledSummary");
        }
        catch (Exception ex)
        {
            ModalDialog.Show(Application.Current.MainWindow, L("IndexingFailedTitle"), ex.Message);
            StatusText = L("IndexingFailed");
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
        if (!ModalDialog.Confirm(ActiveOwner(), L("ClearTitle"),
                L("ClearMsg"), L("ClearConfirm")))
            return;

        _index = null;
        Results.Clear();
        ResultCountText = "";
        try { if (File.Exists(IndexCache.IndexPath)) File.Delete(IndexCache.IndexPath); } catch { }
        IndexSummary = L("NoIndexShort");
        StatusText = L("IndexCleared");
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
            ResultCountText = _query.Length > 0 ? L("BuildFirst") : "";
            return;
        }

        var engine = _engine;
        bool empty = string.IsNullOrWhiteSpace(query);
        bool loadMeta = _settings.ShowSize || _settings.ShowModified || _settings.ShowAttributes;
        var sw = Stopwatch.StartNew();

        var (rows, total) = await Task.Run(() =>
        {
            List<int> hits;
            int tot;
            if (empty)
            {
                // No query: list every file (capped at the display limit).
                tot = index.Count;
                int take = Math.Min(index.Count, ResultLimit);
                hits = new List<int>(take);
                for (int i = 0; i < take; i++) hits.Add(i);
            }
            else
            {
                (hits, tot) = index.Search(query, ResultLimit, engine);
            }

            var list = new List<FileRow>(hits.Count);
            foreach (int i in hits)
            {
                string name = index.GetName(i);
                var row = new FileRow
                {
                    Name = name,
                    Directory = index.GetDirectory(i),
                    FullPath = index.GetFullPath(i),
                    Extension = Path.GetExtension(name)
                };
                if (loadMeta) row.LoadMetadata();
                list.Add(row);
            }
            return (list, tot);
        }, ct);
        sw.Stop();

        if (ct.IsCancellationRequested) return;
        Results.Clear();
        foreach (var r in rows) Results.Add(r);

        if (empty)
        {
            ResultCountText = total > ResultLimit
                ? LF("AllFilesCapped", total.ToString("N0"), ResultLimit.ToString("N0"))
                : LF("AllFiles", total.ToString("N0"));
        }
        else
        {
            string eng = engine == SearchEngine.Masm && MasmAvailable ? "MASM" : "JIT";
            string ms = sw.Elapsed.TotalMilliseconds.ToString("0");
            ResultCountText = total > ResultLimit
                ? LF("ResultCapped", total.ToString("N0"), ResultLimit.ToString("N0"), ms, eng)
                : LF(total == 1 ? "ResultOne" : "ResultMany", total.ToString("N0"), ms, eng);
        }
    }

    // ---------------- benchmark ----------------

    private async Task RunBenchmarkAsync()
    {
        var index = _index;
        if (index == null)
        {
            ModalDialog.Show(Application.Current.MainWindow, L("BenchTitle"), L("BenchNoIndex"));
            return;
        }

        string q = Query;
        if (string.IsNullOrWhiteSpace(q))
        {
            ModalDialog.Show(Application.Current.MainWindow, L("BenchTitle"), L("BenchTypeFirst"));
            return;
        }
        if (WildcardMatcher.HasWildcard(q))
        {
            ModalDialog.Show(Application.Current.MainWindow, L("BenchTitle"), L("BenchWildcard"));
            return;
        }

        StatusText = L("Benchmarking");
        var (jitMs, masmMs, total) = await Task.Run(() =>
        {
            const int iters = 40;
            int t = index.Search(q, ResultLimit, SearchEngine.Jit).total;
            double j = BestOf(() => index.Search(q, ResultLimit, SearchEngine.Jit), iters);
            double m = MasmAvailable ? BestOf(() => index.Search(q, ResultLimit, SearchEngine.Masm), iters) : -1;
            return (j, m, t);
        });
        StatusText = L("IndexReady");

        string msg =
            LF("BenchTerm", q) + "\n" +
            LF("BenchScanned", index.Count.ToString("N0")) + "\n" +
            LF("BenchMatches", total.ToString("N0")) + "\n\n" +
            LF("BenchJit", jitMs) + "\n";

        if (masmMs >= 0)
        {
            msg += LF("BenchMasm", masmMs) + "\n\n";
            if (masmMs > 0 && jitMs > 0)
            {
                double ratio = jitMs / masmMs;
                msg += ratio >= 1.0 ? LF("BenchMasmFaster", ratio) : LF("BenchJitFaster", 1.0 / ratio);
            }
        }
        else
        {
            msg += L("BenchMasmUnavailable") + "\n";
        }
        msg += "\n" + L("BenchFooter");

        ModalDialog.Show(Application.Current.MainWindow, L("BenchTitle"), msg);
    }

    private static double BestOf(Func<(List<int>, int)> action, int iters)
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

    // ---------------- file actions ----------------

    private void OpenFile(object? param)
    {
        if (param is not FileRow row) return;
        try { Process.Start(new ProcessStartInfo(row.FullPath) { UseShellExecute = true }); }
        catch (Exception ex)
        {
            ModalDialog.Show(Application.Current.MainWindow, L("CannotOpenFile"), ex.Message);
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
            ModalDialog.Show(Application.Current.MainWindow, L("CannotOpenFolder"), ex.Message);
        }
    }

    private void OpenWith(object? param)
    {
        if (param is not FileRow row) return;
        try { Process.Start(new ProcessStartInfo(row.FullPath) { UseShellExecute = true, Verb = "openas" }); }
        catch (Exception ex)
        {
            ModalDialog.Show(Application.Current.MainWindow, L("CannotOpenFile"), ex.Message);
        }
    }

    private void ShowProperties(object? param)
    {
        if (param is not FileRow row) return;
        try { Core.ShellOps.ShowFileProperties(row.FullPath); }
        catch (Exception ex)
        {
            ModalDialog.Show(Application.Current.MainWindow, L("CannotOpenFile"), ex.Message);
        }
    }

    private void RunAsAdmin(object? param)
    {
        if (param is not FileRow row) return;
        try { Process.Start(new ProcessStartInfo(row.FullPath) { UseShellExecute = true, Verb = "runas" }); }
        catch (System.ComponentModel.Win32Exception w) when (w.NativeErrorCode == 1223) { /* user declined UAC */ }
        catch (Exception ex)
        {
            ModalDialog.Show(Application.Current.MainWindow, L("CannotOpenFile"), ex.Message);
        }
    }

    private void OpenInTerminal(object? param)
    {
        if (param is not FileRow row) return;
        string dir = row.Directory;
        try { Process.Start(new ProcessStartInfo("wt.exe") { UseShellExecute = true, Arguments = $"-d \"{dir}\"" }); }
        catch
        {
            try { Process.Start(new ProcessStartInfo("powershell.exe") { UseShellExecute = true, WorkingDirectory = dir }); }
            catch (Exception ex)
            {
                ModalDialog.Show(Application.Current.MainWindow, L("CannotOpenFolder"), ex.Message);
            }
        }
    }

    private void FindByType(object? param)
    {
        if (param is not FileRow row) return;
        string ext = Path.GetExtension(row.Name);
        Query = string.IsNullOrEmpty(ext) ? row.Name : "*" + ext; // setter triggers the search
    }

    private void CopyFileToClipboard(object? param)
    {
        if (param is not FileRow row) return;
        try
        {
            var files = new System.Collections.Specialized.StringCollection { row.FullPath };
            Clipboard.SetFileDropList(files);
        }
        catch (Exception ex)
        {
            ModalDialog.Show(Application.Current.MainWindow, L("CannotOpenFile"), ex.Message);
        }
    }

    private static void CopyText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        try { Clipboard.SetText(text); }
        catch { try { Clipboard.SetDataObject(text, true); } catch { /* clipboard busy */ } }
    }

    private void OpenCacheFolder()
    {
        try { Process.Start(new ProcessStartInfo(IndexCache.Directory) { UseShellExecute = true }); }
        catch (Exception ex)
        {
            ModalDialog.Show(Application.Current.MainWindow, L("CannotOpenCache"), ex.Message);
        }
    }

    private void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex)
        {
            ModalDialog.Show(Application.Current.MainWindow, L("CannotOpenFile"), ex.Message);
        }
    }

    private void ShowAbout()
    {
        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        string ver = v is null ? "1.0" : $"{v.Major}.{v.Minor}.{v.Build}";
        string avx = Core.SimdSearch.HardwareAccelerated ? L("AvxEnabled") : L("AvxScalar");
        ModalDialog.Show(Application.Current.MainWindow, L("AboutTitle"), LF("AboutBody", ver, avx));
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
            ModalDialog.Show(Application.Current.MainWindow, L("RestartAdminFailTitle"), ex.Message);
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
