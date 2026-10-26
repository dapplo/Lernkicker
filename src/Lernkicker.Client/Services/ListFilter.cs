namespace Lernkicker.Client.Services;

/// <summary>Search text, subject and grade of a list of tests or learning material; empty means "all".</summary>
public sealed class ListFilter
{
    /// <summary>Grade filter value for quizzes and lessons without a school year (adults).</summary>
    public const int Adults = -1;

    public string Text { get; set; } = "";
    /// <summary>Only used by the library, which lists tests and learning material together.</summary>
    public string Kind { get; set; } = "";
    public string Subject { get; set; } = "";
    public int Grade { get; set; }

    public bool Active => Text.Trim().Length > 0 || Kind != "" || Subject != "" || Grade != 0;

    public bool Matches(string title, string? topic, string subject, string subjectName, int? gradeLevel) =>
        (Subject == "" || Subject == subject)
        && (Grade == 0 || (gradeLevel ?? Adults) == Grade)
        && (Text.Trim() is not { Length: > 0 } text
            || title.Contains(text, StringComparison.CurrentCultureIgnoreCase)
            || topic?.Contains(text, StringComparison.CurrentCultureIgnoreCase) == true
            || subjectName.Contains(text, StringComparison.CurrentCultureIgnoreCase));

    public void Clear()
    {
        Text = "";
        Kind = "";
        Subject = "";
        Grade = 0;
    }
}
