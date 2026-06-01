using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FileFinder.Core;
using Loc = FileFinder.Localization.Localization;

namespace FileFinder.Dialogs;

public partial class StatisticsDialog : Window
{
    private readonly string _cacheDir;
    private static string L(string key) => Loc.Instance[key];

    private StatisticsDialog(string cacheDir)
    {
        InitializeComponent();
        _cacheDir = cacheDir;
        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
    }

    public static void Show(Window? owner, FileIndex? index, string cachePath)
    {
        var d = new StatisticsDialog(Path.GetDirectoryName(cachePath) ?? "")
        {
            Owner = owner ?? Application.Current?.MainWindow
        };
        d.Populate(index, cachePath);
        d.ShowDialog();
    }

    private void Populate(FileIndex? index, string cachePath)
    {
        bool cacheExists = File.Exists(cachePath);
        long cacheBytes = cacheExists ? new FileInfo(cachePath).Length : 0;

        if (index == null)
        {
            SubtitleText.Text = L("StatsNoIndexSubtitle");
            AddRow(L("StatsStatus"), L("StatsNoIndexRow"));
            ExtSection.Visibility = Visibility.Collapsed;
        }
        else
        {
            SubtitleText.Text = string.Format(L("StatsBuilt"),
                index.BuiltUtc.ToLocalTime().ToString("dddd, MMM d yyyy  h:mm tt"));
            AddRow(L("StatsFiles"), index.Count.ToString("N0"));
            AddRow(L("StatsFolders"), index.DirectoryCount.ToString("N0"));
            AddRow(L("StatsDrives"), string.Join("   ", System.Array.ConvertAll(index.Drives, x => x.TrimEnd('\\'))));
            AddRow(L("StatsMemory"), HumanSize(index.ApproxMemoryBytes));
            AddRow(L("StatsSimd"), SimdSearch.HardwareAccelerated ? L("SimdAvx2") : L("SimdScalar"));
            PopulateExtensions(index);
        }

        AddSeparator();
        AddRow(L("StatsCacheFile"), cachePath, mono: true);
        AddRow(L("StatsCacheSize"), cacheExists ? HumanSize(cacheBytes) : L("StatsNotSaved"));
        OpenFolderButton.IsEnabled = Directory.Exists(_cacheDir);
    }

    private void PopulateExtensions(FileIndex index)
    {
        var top = index.TopExtensions(8);
        if (top.Count == 0) { ExtSection.Visibility = Visibility.Collapsed; return; }

        int max = top[0].Count;
        var items = new List<object>();
        foreach (var (ext, count) in top)
        {
            items.Add(new
            {
                Ext = ext == "(no extension)" ? L("StatsNoExt") : ext,
                CountText = count.ToString("N0"),
                BarWidth = max > 0 ? System.Math.Max(2.0, 280.0 * count / max) : 2.0
            });
        }
        ExtList.ItemsSource = items;
    }

    private void AddRow(string label, string value, bool mono = false)
    {
        var grid = new Grid { Margin = new Thickness(0, 5, 0, 5) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var lbl = new TextBlock
        {
            Text = label,
            FontSize = 12.5,
            Foreground = (Brush)FindResource("MutedBrush"),
            VerticalAlignment = VerticalAlignment.Top
        };
        var val = new TextBlock
        {
            Text = value,
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextBrush"),
            TextWrapping = TextWrapping.Wrap
        };
        if (mono) { val.FontFamily = new FontFamily("Consolas"); val.FontWeight = FontWeights.Normal; }

        Grid.SetColumn(lbl, 0);
        Grid.SetColumn(val, 1);
        grid.Children.Add(lbl);
        grid.Children.Add(val);
        RowsPanel.Children.Add(grid);
    }

    private void AddSeparator()
    {
        RowsPanel.Children.Add(new Border
        {
            Height = 1,
            Background = (Brush)FindResource("BorderBrush"),
            Margin = new Thickness(0, 8, 0, 8)
        });
    }

    private static string HumanSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes;
        int u = 0;
        while (v >= 1024 && u < units.Length - 1) { v /= 1024; u++; }
        return $"{v:0.0} {units[u]}  ({bytes:N0} bytes)";
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(_cacheDir) { UseShellExecute = true }); }
        catch { /* folder may have been removed */ }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
