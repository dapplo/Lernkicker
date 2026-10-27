using System.ComponentModel;
using Lernkicker.Application;
using Lernkicker.Application.UseCases;
using Lernkicker.Web.Endpoints;
using Lernkicker.Web.Security;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace Lernkicker.Web.Mcp;

/// <summary>
/// Someone in the family, as the assistant sees it. <c>Role</c>: parent, child or adult (an adult learner without a
/// login). <c>LearnerId</c> is the id for assignments, results and the calendar; a parent has one once they learn.
/// </summary>
public sealed record FamilyMember(string Role, string Name, Guid? ParentId, Guid? LearnerId, int? GradeLevel, string? Theme);

[McpServerToolType]
public sealed class FamilyTools(ChildrenUseCases children, IHttpContextAccessor http)
{
    [McpServerTool(Name = "list_family_members", Title = "List the family", ReadOnly = true, Idempotent = true)]
    [Description("The family's members: parents (parentId, and learnerId once they learn themselves), children (with school year, gradeLevel) and other adult learners. Use the learnerId for assignments, results and the calendar.")]
    public async Task<IReadOnlyList<FamilyMember>> ListFamilyMembers() =>
        (await children.ListMembersAsync(FamilyId)).Select(ToDto).ToList();

    [McpServerTool(Name = "enable_learning", Title = "Let a parent learn", Idempotent = true)]
    [Authorize(Policy = Policies.ApiKeyWrite)]
    [Description("Lets a parent learn too, e.g. one who learns a language: gives them their own learner profile, so quizzes and lessons can be assigned to them. They learn with their own parent login, no extra setup. Returns the parent with their learnerId; does nothing if they already learn.")]
    public async Task<FamilyMember> EnableLearning([Description("The parent's parentId (list_family_members).")] Guid parentId) =>
        ToDto((await children.EnableLearningAsync(FamilyId, parentId)).Value());

    [McpServerTool(Name = "add_child", Title = "Add a child")]
    [Authorize(Policy = Policies.ApiKeyWrite)]
    [Description("Adds a child to the family. A parent then sets the child's PIN and pairs the child's phone in the app.")]
    public async Task<FamilyMember> AddChild(
        [Description("First name, at most 50 characters.")] string name,
        [Description("School year 1-13.")] int? gradeLevel = null,
        [Description("The look of the app: football (default), space or unicorn.")] string? theme = null)
    {
        var child = (await children.CreateAsync(FamilyId, name, gradeLevel, pin: null, isAdult: false, theme)).Value();
        return new FamilyMember(FamilyRoles.Child, child.Name, null, child.Id, child.GradeLevel, child.Theme);
    }

    [McpServerTool(Name = "update_family_member", Title = "Change a learner's profile", Idempotent = true)]
    [Authorize(Policy = Policies.ApiKeyWrite)]
    [Description("Changes the name, school year or look of a learner profile (a child, an adult learner, or a parent's learning profile). Omitted values stay as they are.")]
    public async Task<FamilyMember> UpdateFamilyMember(
        [Description("The learnerId (list_family_members).")] Guid learnerId,
        [Description("New first name.")] string? name = null,
        [Description("New school year 1-13 (children only).")] int? gradeLevel = null,
        [Description("football, space or unicorn.")] string? theme = null)
    {
        var current = (await children.ListAsync()).FirstOrDefault(c => c.Id == learnerId)
            ?? throw McpResults.Failure(AppError.NotFound(ErrorCodes.ChildNotFound));
        (await children.UpdateAsync(learnerId, name ?? current.Name, gradeLevel ?? current.GradeLevel, current.IsAdult, theme)).Check();
        return (await children.ListMembersAsync(FamilyId)).Where(m => m.LearnerId == learnerId).Select(ToDto).Single();
    }

    [McpServerTool(Name = "remove_family_member", Title = "Remove a learner", Destructive = true, Idempotent = true)]
    [Authorize(Policy = Policies.ApiKeyWrite)]
    [Description("Removes a learner profile for good, with its assignments, results and calendar. For a parent this only ends their learning; their parent login stays. Only do this when a parent asked for it.")]
    public async Task<string> RemoveFamilyMember([Description("The learnerId (list_family_members).")] Guid learnerId)
    {
        (await children.DeleteAsync(learnerId)).Check();
        return "removed";
    }

    [McpServerTool(Name = "get_authoring_guide", Title = "How to write quizzes and lessons", ReadOnly = true, Idempotent = true)]
    [Description("The guide for writing quiz/v1 and lesson/v1 files, followed by both JSON schemas. Read it before writing or changing a quiz or lesson.")]
    public static string GetAuthoringGuide([Description("Language of the guide: en or de.")] string language = "en") =>
        FormatEndpoints.Prompt(language == "de" ? "de" : "en");

    private Guid FamilyId => http.HttpContext!.User.RequiredFamily();

    private static FamilyMember ToDto(FamilyMemberSummary m) => new(m.Role, m.Name, m.ParentId, m.LearnerId, m.GradeLevel, m.Theme);
}
