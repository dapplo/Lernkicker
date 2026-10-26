using System.Net.Http.Headers;
using Lernkicker.Contracts;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace Lernkicker.Client.Services;

/// <summary>A chosen file that is larger than the upload allows.</summary>
public sealed class FileTooLargeException(string fileName) : Exception(fileName)
{
    public string FileName { get; } = fileName;
}

/// <summary>
/// Turns the files a parent picked (a JSON file with its pictures, or a zip) into the form the upload endpoints
/// take. Large photos are made smaller in the browser first, which also drops their metadata (e.g. GPS).
/// </summary>
public sealed class UploadFiles(IJSRuntime js)
{
    /// <summary>Photos larger than this are scaled down; smaller pictures and drawings are sent as they are.</summary>
    private const int ShrinkAbove = 400 * 1024;

    /// <summary>Longest side of a scaled photo in pixels.</summary>
    private const int MaxSide = 1600;

    /// <summary>Largest photo the browser reads before scaling it.</summary>
    private const int MaxPhotoBytes = 30 * 1024 * 1024;

    private static readonly HashSet<string> Photos = ["image/jpeg", "image/png", "image/webp"];

    public async Task<MultipartFormDataContent> PrepareAsync(IReadOnlyList<IBrowserFile> files)
    {
        var form = new MultipartFormDataContent();
        foreach (var file in files)
        {
            var bytes = await ReadAsync(file);
            if (Photos.Contains(file.ContentType) && bytes.Length > ShrinkAbove)
            {
                bytes = await js.InvokeAsync<byte[]>("lernkicker.shrinkImage", bytes, file.ContentType, MaxSide);
            }

            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType);
            form.Add(content, "files", file.Name);
        }

        return form;
    }

    private static async Task<byte[]> ReadAsync(IBrowserFile file)
    {
        var max = Photos.Contains(file.ContentType) ? MaxPhotoBytes : ApiLimits.UploadMaxBytes;
        if (file.Size > max)
        {
            throw new FileTooLargeException(file.Name);
        }

        using var buffer = new MemoryStream();
        await using var stream = file.OpenReadStream(max);
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }
}
