using System.IO.Compression;
using System.Text;
using Lernkicker.Application;
using Lernkicker.Application.UseCases;
using Lernkicker.Contracts;
using Lernkicker.Domain.Media;

namespace Lernkicker.Web.Endpoints;

/// <summary>A quiz or lesson upload: the JSON file and the pictures it names.</summary>
public sealed record Upload(string Json, IReadOnlyList<UploadedFile> Files);

/// <summary>
/// Reads a quiz or lesson upload in any of the three forms the API takes: the raw JSON as the body, a zip (as the
/// body or as the only file of a form), or a multipart form with the JSON file and its pictures.
/// </summary>
public static class Uploads
{
    private const string ZipType = "application/zip";

    /// <summary>The upload, or the error code to answer with.</summary>
    public static async Task<(Upload? Upload, int Status, string? Code)> ReadAsync(HttpRequest request, int maxJsonBytes, string tooLargeCode)
    {
        if (request.ContentLength > ApiLimits.UploadMaxBytes)
        {
            return (null, StatusCodes.Status413PayloadTooLarge, ErrorCodes.UploadTooLarge);
        }

        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync();
            if (form.Files is [var only] && IsZip(only.FileName, only.ContentType))
            {
                await using var stream = only.OpenReadStream();
                return await FromZipAsync(stream, maxJsonBytes, tooLargeCode);
            }

            return await FromFilesAsync(form.Files, maxJsonBytes, tooLargeCode);
        }

        if (request.ContentType is { } type && IsZip("", type))
        {
            using var buffer = new MemoryStream();
            if (!await CopyAsync(request.Body, buffer, ApiLimits.UploadMaxBytes))
            {
                return (null, StatusCodes.Status413PayloadTooLarge, ErrorCodes.UploadTooLarge);
            }

            buffer.Position = 0;
            return await FromZipAsync(buffer, maxJsonBytes, tooLargeCode);
        }

        var json = await RequestBody.ReadAsync(request, maxJsonBytes);
        return json is null ? (null, StatusCodes.Status413PayloadTooLarge, tooLargeCode) : (new Upload(json, []), 0, null);
    }

    private static async Task<(Upload?, int, string?)> FromFilesAsync(IFormFileCollection files, int maxJsonBytes, string tooLargeCode)
    {
        if (files.Count > ImageLimits.MaxFiles)
        {
            return (null, StatusCodes.Status400BadRequest, ErrorCodes.UploadTooManyFiles);
        }

        var documents = files.Where(f => IsJson(f.FileName)).ToList();
        if (documents.Count != 1)
        {
            return (null, StatusCodes.Status400BadRequest, documents.Count == 0 ? ErrorCodes.UploadNoDocument : ErrorCodes.UploadSeveralDocuments);
        }

        if (documents[0].Length > maxJsonBytes)
        {
            return (null, StatusCodes.Status413PayloadTooLarge, tooLargeCode);
        }

        var pictures = new List<UploadedFile>();
        foreach (var file in files.Where(f => f != documents[0]))
        {
            await using var stream = file.OpenReadStream();
            pictures.Add(new UploadedFile(file.FileName, await ReadAllAsync(stream)));
        }

        await using var json = documents[0].OpenReadStream();
        return (new Upload(Encoding.UTF8.GetString(await ReadAllAsync(json)), pictures), 0, null);
    }

    /// <summary>The zip holds one JSON file; picture names are taken relative to its folder.</summary>
    private static async Task<(Upload?, int, string?)> FromZipAsync(Stream stream, int maxJsonBytes, string tooLargeCode)
    {
        try
        {
            await using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            var entries = zip.Entries.Where(e => !e.FullName.EndsWith('/') && !IsClutter(e.FullName)).ToList();
            if (entries.Count > ImageLimits.MaxFiles)
            {
                return (null, StatusCodes.Status400BadRequest, ErrorCodes.UploadTooManyFiles);
            }

            if (entries.Any(e => !IsSafePath(e.FullName)))
            {
                return (null, StatusCodes.Status400BadRequest, ErrorCodes.UploadInvalidZip);
            }

            var documents = entries.Where(e => IsJson(e.FullName)).ToList();
            if (documents.Count != 1)
            {
                return (null, StatusCodes.Status400BadRequest, documents.Count == 0 ? ErrorCodes.UploadNoDocument : ErrorCodes.UploadSeveralDocuments);
            }

            var document = documents[0];
            if (document.Length > maxJsonBytes)
            {
                return (null, StatusCodes.Status413PayloadTooLarge, tooLargeCode);
            }

            // The declared sizes can lie; the copy stops at the limit whatever they say
            var folder = document.FullName[..(document.FullName.LastIndexOf('/') + 1)];
            long total = 0;
            var pictures = new List<UploadedFile>();
            string? json = null;
            foreach (var entry in entries)
            {
                await using var content = await entry.OpenAsync();
                using var buffer = new MemoryStream();
                if (!await CopyAsync(content, buffer, ApiLimits.UploadMaxBytes - total))
                {
                    return (null, StatusCodes.Status413PayloadTooLarge, ErrorCodes.UploadTooLarge);
                }

                total += buffer.Length;
                if (entry == document)
                {
                    json = Encoding.UTF8.GetString(buffer.ToArray());
                }
                else
                {
                    var name = entry.FullName.StartsWith(folder, StringComparison.Ordinal) ? entry.FullName[folder.Length..] : entry.FullName;
                    pictures.Add(new UploadedFile(name, buffer.ToArray()));
                }
            }

            return (new Upload(json!, pictures), 0, null);
        }
        catch (InvalidDataException)
        {
            return (null, StatusCodes.Status400BadRequest, ErrorCodes.UploadInvalidZip);
        }
    }

    private static bool IsZip(string fileName, string? contentType) =>
        fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
        || contentType is ZipType or "application/x-zip-compressed" or "application/x-zip";

    private static bool IsJson(string name) => name.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

    /// <summary>Files a Mac or Windows adds to zips on its own.</summary>
    private static bool IsClutter(string path) =>
        path.StartsWith("__MACOSX/", StringComparison.Ordinal) || path.Split('/')[^1] is ".DS_Store" or "Thumbs.db" or "desktop.ini";

    private static bool IsSafePath(string path) =>
        !path.StartsWith('/') && !path.Contains('\\') && !path.Contains(':') && path.Split('/').All(part => part is not ("" or "." or ".."));

    private static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    /// <summary>Copies at most <paramref name="maxBytes"/>; false when there was more.</summary>
    private static async Task<bool> CopyAsync(Stream from, Stream to, long maxBytes)
    {
        var block = new byte[81920];
        long copied = 0;
        int read;
        while ((read = await from.ReadAsync(block)) > 0)
        {
            copied += read;
            if (copied > maxBytes)
            {
                return false;
            }

            await to.WriteAsync(block.AsMemory(0, read));
        }

        return true;
    }

    /// <summary>The quiz or lesson file with its pictures as a zip, in the layout an upload takes.</summary>
    public static IResult Zip(DocumentPackage package)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, $"{package.Id}.json", Encoding.UTF8.GetBytes(package.Json));
            foreach (var file in package.Files)
            {
                Add(zip, file.Name, file.Bytes);
            }
        }

        return Results.File(buffer.ToArray(), ZipType, $"{package.Id}.zip");
    }

    private static void Add(ZipArchive zip, string name, byte[] bytes)
    {
        using var entry = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
        entry.Write(bytes);
    }
}
