using System.ComponentModel;
using Lernkicker.Application.UseCases;
using Lernkicker.Contracts;
using Lernkicker.Web.Endpoints;
using Lernkicker.Web.Security;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace Lernkicker.Web.Mcp;

/// <summary>A child's todo list: the quizzes to practise and the lessons to learn, with due dates and lesson progress.</summary>
public sealed record ChildTodo(IReadOnlyList<AssignmentOverview> Quizzes, IReadOnlyList<LessonAssignmentOverview> Lessons);

[McpServerToolType]
public sealed class AssignmentTools(ChildrenUseCases children, LessonUseCases lessons)
{
    [McpServerTool(Name = "list_assignments", Title = "A child's todo list", ReadOnly = true, Idempotent = true)]
    [Description("What a child has to do: assigned quizzes and lessons with due dates; lessons with how often they were practised and which parts the child knows.")]
    public async Task<ChildTodo> ListAssignments([Description("The learner's id (learnerId from list_family_members).")] Guid childId)
    {
        var lessonAssignments = (await lessons.ListAssignmentsAsync(childId)).Value();
        var quizzes = await children.ListAssignmentsAsync(childId);
        return new ChildTodo(quizzes.Select(a => a.ToDto()).ToList(), lessonAssignments.Select(a => a.ToDto()).ToList());
    }

    [McpServerTool(Name = "assign_quiz", Title = "Give a quiz to a child", Idempotent = true)]
    [Authorize(Policy = Policies.ApiKeyWrite)]
    [Description("Puts the current version of a quiz on a child's todo list, with an optional due date that also shows in the child's calendar. Assigning it again only changes the due date.")]
    public async Task<AssignmentOverview> AssignQuiz(
        [Description("The learner's id (learnerId from list_family_members).")] Guid childId,
        [Description("The quiz id.")] string quizId,
        [Description("Due date, yyyy-MM-dd (e.g. the day of the test); omit for no date.")] DateOnly? dueOn = null) =>
        (await children.AssignAsync(childId, quizId, dueOn)).Value().ToDto();

    [McpServerTool(Name = "unassign_quiz", Title = "Take a quiz off a child's list", Destructive = true, Idempotent = true)]
    [Authorize(Policy = Policies.ApiKeyWrite)]
    [Description("Removes a quiz assignment (the id from list_assignments). Results the child already has stay.")]
    public async Task<string> UnassignQuiz(
        [Description("The learner's id.")] Guid childId,
        [Description("The assignment id (not the quiz id).")] Guid assignmentId)
    {
        (await children.UnassignAsync(childId, assignmentId)).Check();
        return "removed";
    }

    [McpServerTool(Name = "assign_lesson", Title = "Give a lesson to a child", Idempotent = true)]
    [Authorize(Policy = Policies.ApiKeyWrite)]
    [Description("Puts a lesson on a child's list, with an optional due date that also shows in the child's calendar. Assigning it again only changes the due date.")]
    public async Task<LessonAssignmentOverview> AssignLesson(
        [Description("The learner's id (learnerId from list_family_members).")] Guid childId,
        [Description("The lesson id.")] string lessonId,
        [Description("Due date, yyyy-MM-dd; omit for no date.")] DateOnly? dueOn = null) =>
        (await lessons.AssignAsync(childId, lessonId, dueOn)).Value().ToDto();

    [McpServerTool(Name = "unassign_lesson", Title = "Take a lesson off a child's list", Destructive = true, Idempotent = true)]
    [Authorize(Policy = Policies.ApiKeyWrite)]
    [Description("Removes a lesson assignment (the id from list_assignments) together with the child's progress on it.")]
    public async Task<string> UnassignLesson(
        [Description("The learner's id.")] Guid childId,
        [Description("The assignment id (not the lesson id).")] Guid assignmentId)
    {
        (await lessons.UnassignAsync(childId, assignmentId)).Check();
        return "removed";
    }

    [McpServerTool(Name = "list_results", Title = "A child's results", ReadOnly = true, Idempotent = true)]
    [Description("The child's handed-in quizzes, newest first: points, percent and mark (1-6, German school marks).")]
    public async Task<IReadOnlyList<AttemptOverview>> ListResults([Description("The learner's id (learnerId from list_family_members).")] Guid childId) =>
        (await children.ListAttemptsAsync(childId)).Select(a => a.ToDto()).ToList();

    [McpServerTool(Name = "get_result", Title = "One result in detail", ReadOnly = true, Idempotent = true)]
    [Description("One handed-in quiz in detail: every exercise with the child's answers, what was right, the solutions and points. Useful to see what to practise next.")]
    public async Task<QuizResultDto> GetResult(
        [Description("The learner's id.")] Guid childId,
        [Description("The result id (from list_results).")] Guid attemptId) =>
        (await children.AttemptResultAsync(childId, attemptId)).Value().ToDto();
}
