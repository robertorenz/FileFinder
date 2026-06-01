using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FileFinder.Core;
using FileFinder.Localization;

namespace FileFinder.Dialogs;

public partial class PreferencesDialog : Window
{
    private readonly string _originalLanguage;
    private bool _initialized;

    public SearchEngine SelectedEngine { get; private set; }
    public string SelectedLanguage { get; private set; }
    public bool BenchmarkRequested { get; private set; }

    private PreferencesDialog(SearchEngine engine, bool masmAvailable, string language)
    {
        InitializeComponent();
        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };

        _originalLanguage = language;
        SelectedEngine = engine;
        SelectedLanguage = language;

        MasmRadio.IsEnabled = masmAvailable;
        MasmRadio.IsChecked = engine == SearchEngine.Masm && masmAvailable;
        JitRadio.IsChecked = !(engine == SearchEngine.Masm && masmAvailable);

        LanguageCombo.ItemsSource = Localization.Localization.Languages;
        LanguageCombo.SelectedValue = language;
        _initialized = true;
    }

    /// <summary>
    /// Shows the dialog. Returns whether the user saved, and whether they asked
    /// to run the benchmark (which also applies the current selections).
    /// </summary>
    public static (bool Saved, bool Benchmark) Show(Window? owner, ref SearchEngine engine,
        bool masmAvailable, ref string language)
    {
        var d = new PreferencesDialog(engine, masmAvailable, language)
        {
            Owner = owner ?? Application.Current?.MainWindow
        };
        bool saved = d.ShowDialog() == true;
        if (saved)
        {
            engine = d.SelectedEngine;
            language = d.SelectedLanguage;
        }
        return (saved, d.BenchmarkRequested);
    }

    private void Apply()
    {
        SelectedEngine = MasmRadio.IsChecked == true ? SearchEngine.Masm : SearchEngine.Jit;
        SelectedLanguage = LanguageCombo.SelectedValue as string ?? "en";
    }

    private void Benchmark_Click(object sender, RoutedEventArgs e)
    {
        Apply();
        BenchmarkRequested = true;
        DialogResult = true;
        Close();
    }

    // Preview the language live as the user picks it.
    private void Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized) return;
        if (LanguageCombo.SelectedValue is string code)
            Localization.Localization.Instance.CurrentLanguage = code;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Apply();
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        // Revert the live language preview.
        Localization.Localization.Instance.CurrentLanguage = _originalLanguage;
        DialogResult = false;
        Close();
    }
}
