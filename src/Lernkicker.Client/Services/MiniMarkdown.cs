using System.Text.RegularExpressions;

namespace Lernkicker.Client.Services;

/// <summary>
/// The little formatting a lesson text may use: paragraphs (empty line), lists (lines starting with "- "),
/// **bold** and *italic*. Produces plain data that Razor renders, so no HTML from a file ever reaches the page.
/// </summary>
public static partial class MiniMarkdown
{
    public sealed record Span(string Text, bool Bold = false, bool Italic = false);

    /// <summary>A paragraph (one line of spans) or a list (one line of spans per item).</summary>
    public sealed record Part(bool IsList, IReadOnlyList<IReadOnlyList<Span>> Lines);

    public static IReadOnlyList<Part> Parse(string text)
    {
        var parts = new List<Part>();
        foreach (var block in Blank().Split(text.Replace("\r\n", "\n").Trim()))
        {
            var lines = block.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            var paragraph = new List<string>();
            var items = new List<string>();
            foreach (var line in lines)
            {
                if (line.StartsWith("- "))
                {
                    Flush(paragraph, isList: false);
                    items.Add(line[2..].Trim());
                }
                else
                {
                    Flush(items, isList: true);
                    paragraph.Add(line);
                }
            }

            Flush(paragraph, isList: false);
            Flush(items, isList: true);
        }

        return parts;

        void Flush(List<string> lines, bool isList)
        {
            if (lines.Count == 0)
            {
                return;
            }

            parts.Add(isList
                ? new Part(true, lines.Select(Spans).ToList())
                : new Part(false, [Spans(string.Join(' ', lines))]));
            lines.Clear();
        }
    }

    /// <summary>Splits a line into plain, **bold** and *italic* spans; unmatched stars stay as they are.</summary>
    public static IReadOnlyList<Span> Spans(string line)
    {
        var spans = new List<Span>();
        var at = 0;
        foreach (Match m in Emphasis().Matches(line))
        {
            if (m.Index > at)
            {
                spans.Add(new Span(line[at..m.Index]));
            }

            spans.Add(m.Groups["bold"].Success ? new Span(m.Groups["bold"].Value, Bold: true) : new Span(m.Groups["italic"].Value, Italic: true));
            at = m.Index + m.Length;
        }

        if (at < line.Length)
        {
            spans.Add(new Span(line[at..]));
        }

        return spans;
    }

    [GeneratedRegex(@"\n\s*\n")]
    private static partial Regex Blank();

    [GeneratedRegex(@"\*\*(?<bold>[^*]+)\*\*|\*(?<italic>[^*]+)\*")]
    private static partial Regex Emphasis();
}
