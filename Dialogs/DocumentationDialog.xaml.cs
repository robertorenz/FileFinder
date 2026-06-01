using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Loc = FileFinder.Localization.Localization;

namespace FileFinder.Dialogs;

public partial class DocumentationDialog : Window
{
    private const string BaseUrl = "https://github.com/robertorenz/FileFinder/blob/main/";

    private sealed class Section
    {
        public required string Title { get; init; }
        public required List<string> Lines { get; init; }
        public override string ToString() => Title;
    }

    private readonly string _browserUrl;

    private DocumentationDialog()
    {
        InitializeComponent();
        string lang = Loc.Instance.CurrentLanguage;
        _browserUrl = BaseUrl + (lang == "es" ? "DOCS.es.md" : "DOCS.md");

        var sections = SplitIntoSections(LoadDocsText(lang));
        SectionList.ItemsSource = sections;
        if (sections.Count > 0) SectionList.SelectedIndex = 0;
    }

    public static void Show(Window? owner)
    {
        new DocumentationDialog
        {
            Owner = owner ?? Application.Current?.MainWindow
        }.ShowDialog();
    }

    private static string LoadDocsText(string lang)
    {
        string resource = lang == "es" ? "DOCS.es.md" : "DOCS.md";
        try
        {
            var uri = new Uri($"pack://application:,,,/{resource}");
            using var stream = Application.GetResourceStream(uri)!.Stream;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch
        {
            return "# Documentation\n\nThe bundled documentation could not be loaded.";
        }
    }

    /// <summary>Splits the doc into a leading "Overview" plus one entry per `##` heading.</summary>
    private static List<Section> SplitIntoSections(string md)
    {
        string[] all = md.Replace("\r", "").Split('\n');
        var sections = new List<Section>();
        Section? current = null;
        var intro = new List<string>();
        bool inCode = false;

        foreach (string line in all)
        {
            string t = line.TrimStart();
            if (t.StartsWith("```")) inCode = !inCode;

            if (!inCode && t.StartsWith("## "))
            {
                current = new Section { Title = t[3..].Trim(), Lines = new List<string> { line } };
                sections.Add(current);
            }
            else if (current == null)
            {
                // Drop the auto-TOC bullet links — the left panel replaces them.
                if (!(t.StartsWith("- [") && t.Contains("](#"))) intro.Add(line);
            }
            else
            {
                current.Lines.Add(line);
            }
        }

        if (intro.Exists(l => l.Trim().Length > 0))
            sections.Insert(0, new Section { Title = Loc.Instance["DocsOverview"], Lines = intro });

        return sections;
    }

    /// <summary>Test hook: load each language, split, and render every section. Returns total blocks.</summary>
    internal static int RenderAllSectionsForTest()
    {
        int blocks = 0;
        foreach (string lang in new[] { "en", "es" })
            foreach (var s in SplitIntoSections(LoadDocsText(lang)))
                blocks += MarkdownFlow.ToFlowDocument(s.Lines).Blocks.Count;
        return blocks;
    }

    private void SectionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SectionList.SelectedItem is Section s)
            Viewer.Document = MarkdownFlow.ToFlowDocument(s.Lines);
    }

    private void OpenInBrowser_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(_browserUrl) { UseShellExecute = true }); } catch { }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
