using System.ComponentModel;
using System.Security.Claims;
using Lernkicker.Application.UseCases;
using Lernkicker.Contracts;
using Lernkicker.Web.Endpoints;
using Lernkicker.Web.Security;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace Lernkicker.Web.Mcp;

[McpServerToolType]
public sealed class QuizTools(QuizLibraryUseCases library, IHttpContextAccessor http)
{
    [McpServerTool(Name = "list_quizzes", Title = "List quizzes", ReadOnly = true, Idempotent = true)]
    [Description("The family's quizzes (current versions): id, title, subject, school year, topic, points.")]
    public async Task<IReadOnlyList<QuizOverview>> ListQuizzes() =>
        (await library.ListAsync()).Select(q => q.ToDto()).ToList();

    [McpServerTool(Name = "get_quiz", Title = "Download a quiz", ReadOnly = true, Idempotent = true)]
    [Description("The quiz/v1 JSON file of a quiz exactly as uploaded, optionally with its pictures (base64), ready to change and upload again.")]
    public async Task<DocumentFile> GetQuiz(
        [Description("The quiz id (the \"id\" in the file).")] string quizId,
        [Description("Also return the pictures the file uses.")] bool includePictures = false) =>
        (await library.PackageAsync(quizId)).Value().ToFile(includePictures);

    [McpServerTool(Name = "validate_quiz", Title = "Check a quiz", ReadOnly = true, Idempotent = true)]
    [Description("Checks a quiz/v1 file (schema and content rules) and its pictures like an upload, without storing it. Fails with the list of issues.")]
    public ValidationResult ValidateQuiz(
        [Description("The quiz/v1 JSON file as text.")] string json,
        [Description("Pictures the file uses: name = the src in the file, base64 = the file's bytes (PNG, JPEG, GIF, WebP or SVG).")] IReadOnlyList<PictureFile>? pictures = null) =>
        new(true, library.Validate(json, McpResults.Files(pictures)).Value());

    [McpServerTool(Name = "upload_quiz", Title = "Upload a quiz", Idempotent = true)]
    [Authorize(Policy = Policies.ApiKeyWrite)]
    [Description("Stores a quiz/v1 file in the family's library. A quiz with the same id gets a new version; open assignments move to it, results stay. Fails with the list of issues when the file is invalid.")]
    public async Task<QuizOverview> UploadQuiz(
        [Description("The quiz/v1 JSON file as text.")] string json,
        [Description("Pictures the file uses: name = the src in the file, base64 = the file's bytes (PNG, JPEG, GIF, WebP or SVG).")] IReadOnlyList<PictureFile>? pictures = null) =>
        (await library.UploadAsync(User.RequiredFamily(), json, McpResults.Files(pictures))).Value().ToDto();

    [McpServerTool(Name = "delete_quiz", Title = "Delete a quiz", Destructive = true)]
    [Authorize(Policy = Policies.ApiKeyWrite)]
    [Description("Removes a quiz and its assignments. A quiz with results is archived instead, so the results stay.")]
    public async Task<QuizDeletedResponse> DeleteQuiz([Description("The quiz id.")] string quizId) =>
        new((await library.DeleteAsync(quizId)).Value().Archived);

    private ClaimsPrincipal User => http.HttpContext!.User;
}
