using System.ComponentModel;
using Lernkicker.Application.UseCases;
using Lernkicker.Web.Endpoints;
using ModelContextProtocol.Server;

namespace Lernkicker.Web.Mcp;

/// <summary>A child or adult learner of the family, as the assistant sees it.</summary>
public sealed record ChildInfo(Guid Id, string Name, int? GradeLevel, bool IsAdult);

[McpServerToolType]
public sealed class FamilyTools(ChildrenUseCases children)
{
    [McpServerTool(Name = "list_children", Title = "List children", ReadOnly = true, Idempotent = true)]
    [Description("The family's children and adult learner profiles with their ids and school year (gradeLevel). Use the id for assignments, results and the calendar.")]
    public async Task<IReadOnlyList<ChildInfo>> ListChildren() =>
        (await children.ListAsync()).Select(c => new ChildInfo(c.Id, c.Name, c.GradeLevel, c.IsAdult)).ToList();

    [McpServerTool(Name = "get_authoring_guide", Title = "How to write quizzes and lessons", ReadOnly = true, Idempotent = true)]
    [Description("The guide for writing quiz/v1 and lesson/v1 files, followed by both JSON schemas. Read it before writing or changing a quiz or lesson.")]
    public static string GetAuthoringGuide([Description("Language of the guide: en or de.")] string language = "en") =>
        FormatEndpoints.Prompt(language == "de" ? "de" : "en");
}
