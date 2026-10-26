using System.ComponentModel;
using Lernkicker.Application.UseCases;
using Lernkicker.Contracts;
using Lernkicker.Web.Endpoints;
using Lernkicker.Web.Security;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace Lernkicker.Web.Mcp;

[McpServerToolType]
public sealed class LessonTools(LessonUseCases lessons, IHttpContextAccessor http)
{
    [McpServerTool(Name = "list_lessons", Title = "List lessons", ReadOnly = true, Idempotent = true)]
    [Description("The family's lessons (learning material): id, title, subject, school year, topic, number of parts.")]
    public async Task<IReadOnlyList<LessonOverview>> ListLessons() =>
        (await lessons.ListAsync()).Select(l => l.ToDto()).ToList();

    [McpServerTool(Name = "get_lesson", Title = "Download a lesson", ReadOnly = true, Idempotent = true)]
    [Description("The lesson/v1 JSON file of a lesson exactly as uploaded, optionally with its pictures (base64), ready to change and upload again.")]
    public async Task<DocumentFile> GetLesson(
        [Description("The lesson id (the \"id\" in the file).")] string lessonId,
        [Description("Also return the pictures the file uses.")] bool includePictures = false) =>
        (await lessons.PackageAsync(lessonId)).Value().ToFile(includePictures);

    [McpServerTool(Name = "validate_lesson", Title = "Check a lesson", ReadOnly = true, Idempotent = true)]
    [Description("Checks a lesson/v1 file (schema and content rules) and its pictures like an upload, without storing it. Fails with the list of issues.")]
    public ValidationResult ValidateLesson(
        [Description("The lesson/v1 JSON file as text.")] string json,
        [Description("Pictures the file uses: name = the src in the file, base64 = the file's bytes (PNG, JPEG, GIF, WebP or SVG).")] IReadOnlyList<PictureFile>? pictures = null) =>
        new(true, lessons.Validate(json, McpResults.Files(pictures)).Value());

    [McpServerTool(Name = "upload_lesson", Title = "Upload a lesson", Idempotent = true)]
    [Authorize(Policy = Policies.ApiKeyWrite)]
    [Description("Stores a lesson/v1 file in the family's library. A lesson with the same id is replaced; assignments and progress stay. Fails with the list of issues when the file is invalid.")]
    public async Task<LessonOverview> UploadLesson(
        [Description("The lesson/v1 JSON file as text.")] string json,
        [Description("Pictures the file uses: name = the src in the file, base64 = the file's bytes (PNG, JPEG, GIF, WebP or SVG).")] IReadOnlyList<PictureFile>? pictures = null) =>
        (await lessons.UploadAsync(http.HttpContext!.User.RequiredFamily(), json, McpResults.Files(pictures))).Value().ToDto();

    [McpServerTool(Name = "delete_lesson", Title = "Delete a lesson", Destructive = true)]
    [Authorize(Policy = Policies.ApiKeyWrite)]
    [Description("Removes a lesson together with its assignments and the children's progress.")]
    public async Task<string> DeleteLesson([Description("The lesson id.")] string lessonId)
    {
        (await lessons.DeleteAsync(lessonId)).Check();
        return "deleted";
    }
}
