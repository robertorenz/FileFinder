using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace FileFinder.Dialogs;

/// <summary>
/// Minimal Markdown → WPF FlowDocument renderer for the in-app documentation.
/// Supports headings, paragraphs, bullet lists, tables, fenced code blocks, and
/// inline **bold**, `code`, and [links](url). Good enough for DOCS.md.
/// </summary>
internal static class MarkdownFlow
{
    private static Brush Text => Res("TextBrush", "#1E293B");
    private static Brush Muted => Res("MutedBrush", "#64748B");
    private static Brush Primary => Res("PrimaryBrush", "#2563EB");
    private static Brush Border => Res("BorderBrush", "#E2E8F0");
    private static readonly Brush CodeBg = (Brush)new BrushConverter().ConvertFromString("#F1F5F9")!;
    private static readonly FontFamily Mono = new("Consolas, Cascadia Mono, monospace");

    private static Brush Res(string key, string fallback)
        => Application.Current?.TryFindResource(key) as Brush
           ?? (Brush)new BrushConverter().ConvertFromString(fallback)!;

    public static FlowDocument ToFlowDocument(IReadOnlyList<string> lines)
    {
        var doc = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13.5,
            Foreground = Text,
            PagePadding = new Thickness(28, 22, 28, 28),
            TextAlignment = TextAlignment.Left,
        };

        int i = 0;
        while (i < lines.Count)
        {
            string raw = lines[i];
            string line = raw.TrimEnd();
            string t = line.TrimStart();

            if (t.Length == 0) { i++; continue; }

            // Fenced code block
            if (t.StartsWith("```"))
            {
                var code = new StringBuilder();
                i++;
                while (i < lines.Count && !lines[i].TrimStart().StartsWith("```")) { code.AppendLine(lines[i]); i++; }
                i++; // closing fence
                doc.Blocks.Add(CodeBlock(code.ToString().TrimEnd('\n', '\r')));
                continue;
            }

            // Headings
            if (t.StartsWith("### ")) { doc.Blocks.Add(Heading(t[4..], 14, FontWeights.SemiBold, 12, 4, false)); i++; continue; }
            if (t.StartsWith("## ")) { doc.Blocks.Add(Heading(t[3..], 17, FontWeights.SemiBold, 16, 8, true)); i++; continue; }
            if (t.StartsWith("# ")) { doc.Blocks.Add(Heading(t[2..], 22, FontWeights.Bold, 0, 10, false)); i++; continue; }

            // Horizontal rule
            if (t == "---") { doc.Blocks.Add(Rule()); i++; continue; }

            // Block quote
            if (t.StartsWith("> "))
            {
                var p = new Paragraph { Margin = new Thickness(0, 4, 0, 10), Padding = new Thickness(12, 6, 12, 6) };
                p.Background = CodeBg;
                p.Foreground = Muted;
                p.Inlines.AddRange(Inlines(t[2..]));
                doc.Blocks.Add(p);
                i++;
                continue;
            }

            // Table (current line has pipes and the next is a separator row)
            if (t.Contains('|') && i + 1 < lines.Count && IsTableSeparator(lines[i + 1]))
            {
                var tableLines = new List<string>();
                while (i < lines.Count && lines[i].Contains('|')) { tableLines.Add(lines[i]); i++; }
                doc.Blocks.Add(BuildTable(tableLines));
                continue;
            }

            // Bullet list
            if (t.StartsWith("- ") || t.StartsWith("* "))
            {
                var list = new List { MarkerStyle = TextMarkerStyle.Disc, Margin = new Thickness(0, 2, 0, 10), Padding = new Thickness(22, 0, 0, 0) };
                while (i < lines.Count)
                {
                    string lt = lines[i].TrimStart();
                    if (!(lt.StartsWith("- ") || lt.StartsWith("* "))) break;
                    var item = new ListItem();
                    var p = new Paragraph { Margin = new Thickness(0, 1, 0, 1) };
                    p.Inlines.AddRange(Inlines(lt[2..]));
                    item.Blocks.Add(p);
                    list.ListItems.Add(item);
                    i++;
                }
                doc.Blocks.Add(list);
                continue;
            }

            // Paragraph
            var para = new Paragraph { Margin = new Thickness(0, 0, 0, 9), LineHeight = 20 };
            para.Inlines.AddRange(Inlines(t));
            doc.Blocks.Add(para);
            i++;
        }

        return doc;
    }

    private static Paragraph Heading(string text, double size, FontWeight weight, double top, double bottom, bool underline)
    {
        var p = new Paragraph
        {
            FontSize = size,
            FontWeight = weight,
            Foreground = Text,
            Margin = new Thickness(0, top, 0, bottom),
        };
        if (underline) { p.BorderBrush = Border; p.BorderThickness = new Thickness(0, 0, 0, 1); p.Padding = new Thickness(0, 0, 0, 6); }
        p.Inlines.AddRange(Inlines(text));
        return p;
    }

    private static Block Rule() => new BlockUIContainer(new System.Windows.Controls.Border
    {
        Height = 1,
        Background = Border,
        Margin = new Thickness(0, 8, 0, 8),
    });

    private static Paragraph CodeBlock(string code)
    {
        var p = new Paragraph
        {
            FontFamily = Mono,
            FontSize = 12.5,
            Background = CodeBg,
            Foreground = Text,
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 2, 0, 12),
        };
        var parts = code.Replace("\r", "").Split('\n');
        for (int k = 0; k < parts.Length; k++)
        {
            p.Inlines.Add(new Run(parts[k]));
            if (k < parts.Length - 1) p.Inlines.Add(new LineBreak());
        }
        return p;
    }

    private static bool IsTableSeparator(string line)
    {
        string s = line.Trim();
        if (!s.Contains('-') || !s.Contains('|')) return false;
        foreach (char c in s) if (c is not ('|' or '-' or ':' or ' ')) return false;
        return true;
    }

    private static string[] SplitRow(string line)
    {
        string s = line.Trim();
        if (s.StartsWith('|')) s = s[1..];
        if (s.EndsWith('|')) s = s[..^1];
        var cells = s.Split('|');
        for (int i = 0; i < cells.Length; i++) cells[i] = cells[i].Trim();
        return cells;
    }

    private static Table BuildTable(List<string> lines)
    {
        var header = SplitRow(lines[0]);
        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 4, 0, 12) };
        for (int c = 0; c < header.Length; c++)
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

        var group = new TableRowGroup();

        // header row
        var hr = new TableRow();
        foreach (var h in header) hr.Cells.Add(Cell(h, bold: true, header: true));
        group.Rows.Add(hr);

        // data rows (skip the separator line at index 1)
        for (int r = 2; r < lines.Count; r++)
        {
            var cells = SplitRow(lines[r]);
            var row = new TableRow();
            for (int c = 0; c < header.Length; c++)
                row.Cells.Add(Cell(c < cells.Length ? cells[c] : "", bold: false, header: false));
            group.Rows.Add(row);
        }

        table.RowGroups.Add(group);
        return table;
    }

    private static TableCell Cell(string text, bool bold, bool header)
    {
        var p = new Paragraph { Margin = new Thickness(0) };
        if (bold) p.FontWeight = FontWeights.SemiBold;
        p.Inlines.AddRange(Inlines(text));
        return new TableCell(p)
        {
            Padding = new Thickness(10, 7, 10, 7),
            BorderBrush = Border,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Foreground = header ? Muted : Text,
        };
    }

    private static List<Inline> Inlines(string s)
    {
        var result = new List<Inline>();
        var buf = new StringBuilder();
        void Flush() { if (buf.Length > 0) { result.Add(new Run(buf.ToString())); buf.Clear(); } }

        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];

            // **bold**
            if (c == '*' && i + 1 < s.Length && s[i + 1] == '*')
            {
                int end = s.IndexOf("**", i + 2, StringComparison.Ordinal);
                if (end > 0) { Flush(); result.Add(new Run(s[(i + 2)..end]) { FontWeight = FontWeights.SemiBold }); i = end + 2; continue; }
            }

            // `code`
            if (c == '`')
            {
                int end = s.IndexOf('`', i + 1);
                if (end > 0) { Flush(); result.Add(new Run(s[(i + 1)..end]) { FontFamily = Mono, Background = CodeBg }); i = end + 1; continue; }
            }

            // [text](url)
            if (c == '[')
            {
                int close = s.IndexOf(']', i + 1);
                if (close > 0 && close + 1 < s.Length && s[close + 1] == '(')
                {
                    int paren = s.IndexOf(')', close + 2);
                    if (paren > 0)
                    {
                        Flush();
                        string label = s[(i + 1)..close];
                        string url = s[(close + 2)..paren];
                        if (url.StartsWith('#'))
                        {
                            // in-page anchor: render as plain emphasized text (the left panel navigates)
                            result.Add(new Run(label) { Foreground = Primary });
                        }
                        else
                        {
                            var link = new Hyperlink(new Run(label)) { Foreground = Primary };
                            try { link.NavigateUri = new Uri(url); } catch { }
                            link.RequestNavigate += (_, e) =>
                            {
                                try { Process.Start(new ProcessStartInfo(e.Uri.ToString()) { UseShellExecute = true }); } catch { }
                                e.Handled = true;
                            };
                            result.Add(link);
                        }
                        i = paren + 1;
                        continue;
                    }
                }
            }

            buf.Append(c);
            i++;
        }

        Flush();
        return result;
    }
}
