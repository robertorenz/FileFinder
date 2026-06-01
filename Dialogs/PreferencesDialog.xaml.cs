using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FileFinder.Core;
using Loc = FileFinder.Localization.Localization;

namespace FileFinder.Dialogs;

/// <summary>What the user asked for when the Preferences dialog closed.</summary>
public enum PrefResult { Cancel, Save, Build, Clear, Benchmark }

public partial class PreferencesDialog : Window
{
    private readonly string _originalLanguage;
    private readonly Action? _onRestartAdmin;
    private readonly Action? _onClear;
    private readonly AppSettings _settings;
    private readonly Dictionary<object, bool> _driveSnapshot = new();
    private readonly (bool Folder, bool Type, bool Size, bool Modified, bool Attributes) _columnSnapshot;
    private bool _initialized;

    public SearchEngine SelectedEngine { get; private set; }
    public string SelectedLanguage { get; private set; }
    public PrefResult Result { get; private set; } = PrefResult.Cancel;

    private PreferencesDialog(SearchEngine engine, bool masmAvailable, string language,
        IEnumerable drives, bool isElevated, Action? onRestartAdmin, Action? onClear, AppSettings settings)
    {
        InitializeComponent();
        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };

        _originalLanguage = language;
        _onRestartAdmin = onRestartAdmin;
        _onClear = onClear;
        _settings = settings;
        SelectedEngine = engine;
        SelectedLanguage = language;

        // Columns bind two-way to the settings POCO; snapshot for Cancel.
        ColumnsPanel.DataContext = settings;
        _columnSnapshot = (settings.ShowFolder, settings.ShowType, settings.ShowSize,
            settings.ShowModified, settings.ShowAttributes);

        MasmRadio.IsEnabled = masmAvailable;
        MasmRadio.IsChecked = engine == SearchEngine.Masm && masmAvailable;
        JitRadio.IsChecked = !(engine == SearchEngine.Masm && masmAvailable);

        DrivesList.ItemsSource = drives;
        // Snapshot drive selection so Cancel can revert it.
        foreach (var item in drives)
            if (item is Models.DriveItem di) _driveSnapshot[di] = di.IsSelected;

        AdminText.Text = isElevated ? Loc.Instance["AdminElevated"] : Loc.Instance["AdminStandard"];
        RestartAdminButton.Visibility = isElevated ? Visibility.Collapsed : Visibility.Visible;

        LanguageCombo.ItemsSource = Loc.Languages;
        LanguageCombo.SelectedValue = language;
        _initialized = true;
    }

    /// <summary>Shows the dialog. Engine/language are written back unless the user cancelled.</summary>
    public static PrefResult Show(Window? owner, ref SearchEngine engine, bool masmAvailable,
        ref string language, IEnumerable drives, bool isElevated, Action? onRestartAdmin,
        Action? onClear, AppSettings settings)
    {
        var d = new PreferencesDialog(engine, masmAvailable, language, drives, isElevated,
            onRestartAdmin, onClear, settings)
        {
            Owner = owner ?? Application.Current?.MainWindow
        };
        d.ShowDialog();
        if (d.Result != PrefResult.Cancel)
        {
            engine = d.SelectedEngine;
            language = d.SelectedLanguage;
        }
        return d.Result;
    }

    private void Apply()
    {
        SelectedEngine = MasmRadio.IsChecked == true ? SearchEngine.Masm : SearchEngine.Jit;
        SelectedLanguage = LanguageCombo.SelectedValue as string ?? "en";
    }

    private void Close(PrefResult result)
    {
        Apply();
        Result = result;
        DialogResult = true;
        Close();
    }

    private void Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized) return;
        if (LanguageCombo.SelectedValue is string code)
            Loc.Instance.CurrentLanguage = code;
    }

    private void RestartAdmin_Click(object sender, RoutedEventArgs e) => _onRestartAdmin?.Invoke();

    private void Build_Click(object sender, RoutedEventArgs e) => Close(PrefResult.Build);
    // Clear happens in place so the dialog stays open to reselect drives / rebuild.
    private void Clear_Click(object sender, RoutedEventArgs e) => _onClear?.Invoke();
    private void Benchmark_Click(object sender, RoutedEventArgs e) => Close(PrefResult.Benchmark);
    private void Save_Click(object sender, RoutedEventArgs e) => Close(PrefResult.Save);

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        // Revert live previews: language + drive selection + columns.
        Loc.Instance.CurrentLanguage = _originalLanguage;
        foreach (var kv in _driveSnapshot)
            if (kv.Key is Models.DriveItem di) di.IsSelected = kv.Value;
        (_settings.ShowFolder, _settings.ShowType, _settings.ShowSize,
         _settings.ShowModified, _settings.ShowAttributes) = _columnSnapshot;

        Result = PrefResult.Cancel;
        DialogResult = false;
        Close();
    }
}
