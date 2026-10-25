using System.Reflection;

namespace Lernkicker.Web.Tests;

/// <summary>
/// Guards the hexagon: the domain and the use cases depend on nothing but the BCL and each other; adapters
/// depend inwards, never on each other (docs/architecture.md).
/// </summary>
public class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(Domain.Quizzes.Quiz).Assembly;
    private static readonly Assembly Application = typeof(Application.Result).Assembly;
    private static readonly Assembly Contracts = typeof(Contracts.ApiError).Assembly;
    private static readonly Assembly QuizFormat = typeof(QuizFormat.QuizJson).Assembly;
    private static readonly Assembly Persistence = typeof(Persistence.LernkickerDbContext).Assembly;
    private static readonly Assembly Calendar = typeof(Calendar.IcsCalendarSource).Assembly;

    [Fact]
    public void Domain_depends_only_on_the_bcl() =>
        Assert.Empty(NonBcl(Domain));

    [Fact]
    public void Application_depends_only_on_the_domain() =>
        Assert.Equal(["Lernkicker.Domain"], NonBcl(Application));

    [Fact]
    public void Contracts_depend_on_nothing() =>
        Assert.Empty(NonBcl(Contracts));

    [Fact]
    public void Quiz_format_adapter_knows_only_the_core_and_its_json_schema_library() =>
        Assert.Equal(["JsonSchema.Net", "Lernkicker.Application", "Lernkicker.Domain"], NonBcl(QuizFormat).Where(n => !n.StartsWith("Json.More") && !n.StartsWith("JsonPointer")));

    [Fact]
    public void Persistence_adapter_does_not_know_other_adapters() =>
        Assert.DoesNotContain(NonBcl(Persistence), n => n is "Lernkicker.Web" or "Lernkicker.QuizFormat" or "Lernkicker.Contracts");

    [Fact]
    public void Calendar_adapter_knows_only_the_core_and_its_calendar_libraries() =>
        Assert.Equal(["Ical.Net", "Lernkicker.Application", "Lernkicker.Domain", "NodaTime"], NonBcl(Calendar));

    [Fact]
    public void Shared_kinds_match_the_application()
    {
        Assert.Equal(Lernkicker.Application.SharedKinds.Quiz, Lernkicker.Contracts.SharedKinds.Quiz);
        Assert.Equal(Lernkicker.Application.SharedKinds.Lesson, Lernkicker.Contracts.SharedKinds.Lesson);
    }

    [Fact]
    public void Client_upload_limits_match_the_domain()
    {
        Assert.Equal(Lernkicker.Domain.Quizzes.QuizLimits.MaxBytes, Lernkicker.Contracts.ApiLimits.QuizMaxBytes);
        Assert.Equal(Lernkicker.Domain.Lessons.LessonLimits.MaxBytes, Lernkicker.Contracts.ApiLimits.LessonMaxBytes);
    }

    // Everything that is not part of .NET itself
    private static List<string> NonBcl(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n != "netstandard" && !n.StartsWith("System") && !n.StartsWith("Microsoft.") || n.StartsWith("Microsoft.EntityFrameworkCore") || n.StartsWith("Microsoft.AspNetCore"))
            .Order()
            .ToList();
}
