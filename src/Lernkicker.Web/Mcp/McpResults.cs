using System.Text.Json;
using Lernkicker.Application;
using Lernkicker.Application.UseCases;
using Lernkicker.Contracts;
using ModelContextProtocol;

namespace Lernkicker.Web.Mcp;

/// <summary>A picture that belongs to a quiz or lesson file: <c>Name</c> is the <c>src</c> the file uses, <c>Base64</c> its bytes.</summary>
public sealed record PictureFile(string Name, string Base64);

/// <summary>A quiz or lesson file with its pictures.</summary>
public sealed record DocumentFile(string Id, string Json, IReadOnlyList<PictureFile> Pictures);

/// <summary>Result of a check without storing: <c>Id</c> is the id the file would be stored under.</summary>
public sealed record ValidationResult(bool Valid, string Id);

/// <summary>
/// Turns use case results into tool results. A failure becomes a tool error carrying the stable error code and
/// the issues as JSON, like the HTTP API's error body, so the assistant can fix the file and try again.
/// </summary>
internal static class McpResults
{
    public static T Value<T>(this Result<T> result) => result.IsSuccess ? result.Value : throw Failure(result.Error!);

    public static void Check(this Result result)
    {
        if (!result.IsSuccess)
        {
            throw Failure(result.Error!);
        }
    }

    public static McpException Failure(AppError error) =>
        new(JsonSerializer.Serialize(new ApiError(error.Code, error.Issues?.Select(i => new ApiIssue(i.Code, i.Path, i.Args)).ToList()), ApiJson.Options));

    public static McpException Failure(string code) => Failure(AppError.Invalid(code));

    /// <summary>The pictures of an upload; a name or content the server can't read fails the call.</summary>
    public static IReadOnlyList<UploadedFile> Files(IReadOnlyList<PictureFile>? pictures)
    {
        if (pictures is null)
        {
            return [];
        }

        if (pictures.Count > Domain.Media.ImageLimits.MaxFiles)
        {
            throw Failure(ErrorCodes.UploadTooManyFiles);
        }

        var files = new List<UploadedFile>();
        long total = 0;
        foreach (var picture in pictures)
        {
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(picture.Base64);
            }
            catch (FormatException)
            {
                throw Failure(ErrorCodes.UploadInvalidPicture);
            }

            total += bytes.Length;
            if (total > ApiLimits.UploadMaxBytes)
            {
                throw Failure(new AppError(ErrorKind.TooLarge, ErrorCodes.UploadTooLarge));
            }

            files.Add(new UploadedFile(picture.Name, bytes));
        }

        return files;
    }

    public static DocumentFile ToFile(this DocumentPackage package, bool withPictures) =>
        new(package.Id, package.Json, withPictures ? package.Files.Select(f => new PictureFile(f.Name, Convert.ToBase64String(f.Bytes))).ToList() : []);
}
