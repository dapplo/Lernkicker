using Lernkicker.Contracts;

namespace Lernkicker.Client.Services;

/// <summary>Filters the entries of a lookup block: every word of the query must appear in the name or the text.</summary>
public static class EntrySearch
{
    public static IEnumerable<EntryDto> Filter(IEnumerable<EntryDto> entries, string? query)
    {
        var words = (query ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length == 0
            ? entries
            : entries.Where(e => words.All(w => e.Name.Contains(w, StringComparison.CurrentCultureIgnoreCase) || e.Text.Contains(w, StringComparison.CurrentCultureIgnoreCase)));
    }
}
