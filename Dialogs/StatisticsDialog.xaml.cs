using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FileFinder.Core;

namespace FileFinder.Dialogs;

public partial class StatisticsDialog : Window
{
    private readonly string _cacheDir;

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
            SubtitleText.Text = "No index is currently loaded.";
            AddRow("Status", "No index built yet — use Build Index first.");
            ExtSection.Visibility = Visibility.Collapsed;
        }
        else
        {
            SubtitleText.Text = $"Built {index.BuiltUtc.ToLocalTime():dddd, MMM d yyyy  h:mm tt}";
            AddRow("Files indexed", index.Count.ToString("N0"));
            AddRow("Folders", index.DirectoryCount.ToString("N0"));
            AddRow("Drives", string.Join("   ", System.Array.ConvertAll(index.Drives, x => x.TrimEnd('\\'))));
            AddRow("Memory in use (app)", HumanSize(index.ApproxMemoryBytes));
            AddRow("SIMD acceleration", SimdSearch.HardwareAccelerated ? "AVX2 (hardware)" : "Scalar fallback");
            PopulateExtensions(index);
        }

        AddSeparator();
        AddRow("Cache file", cachePath, mono: true);
        AddRow("Cache size on disk", cacheExists ? HumanSize(cacheBytes) : "Not saved yet");
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
                Ext = ext,
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
