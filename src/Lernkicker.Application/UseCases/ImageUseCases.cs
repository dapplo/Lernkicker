using System.Security.Cryptography;
using Lernkicker.Application.Ports;
using Lernkicker.Domain.Media;
using Lernkicker.Domain.Quizzes;

namespace Lernkicker.Application.UseCases;

/// <summary>A file uploaded together with a quiz or lesson; <c>Name</c> is its path relative to the JSON file.</summary>
public sealed record UploadedFile(string Name, byte[] Bytes);

/// <summary>A picture of a quiz or lesson, checked and ready to store.</summary>
public sealed record AttachedImage(string Src, string Hash, string ContentType, byte[] Bytes);

/// <summary>A quiz or lesson file with its pictures, as uploaded (for downloading it again).</summary>
public sealed record DocumentPackage(string Id, string Json, IReadOnlyList<UploadedFile> Files);

/// <summary>
/// The pictures of quizzes and lessons: checking the uploaded files against the <c>src</c> names, storing them
/// once per content hash, and handing them out to whoever may see a quiz or lesson that uses them.
/// </summary>
public sealed class ImageUseCases(IImageStore store, ISvgSanitizer svg, TimeProvider time)
{
    private const int MegaByte = 1024 * 1024;

    /// <summary>
    /// Finds the file for every picture the document names: by its path, or else by its file name alone (several
    /// files picked in the browser arrive without folders). Reports missing, unknown and too large files. SVG files
    /// are stored cleaned (see <see cref="ISvgSanitizer"/>).
    /// </summary>
    public (IReadOnlyList<AttachedImage> Images, IReadOnlyList<QuizIssue> Issues) Attach(IEnumerable<ImageUse> uses, IReadOnlyList<UploadedFile> files)
    {
        var byPath = files.GroupBy(f => Normalize(f.Name)).ToDictionary(g => g.Key, g => g.First());
        var byName = files.GroupBy(f => FileName(f.Name)).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single());

        var images = new Dictionary<string, AttachedImage>();
        var issues = new List<QuizIssue>();
        var seen = new HashSet<string>();
        foreach (var use in uses)
        {
            var src = use.Image.Src;
            if (!seen.Add(src))
            {
                continue;
            }

            var at = $"{use.Path}/src";
            var file = byPath.GetValueOrDefault(Normalize(src)) ?? byName.GetValueOrDefault(FileName(src));
            if (file is null)
            {
                issues.Add(QuizIssue.Of(QuizIssueCodes.MissingImage, at, ("src", src)));
            }
            else if (file.Bytes.Length > ImageLimits.MaxBytes)
            {
                issues.Add(QuizIssue.Of(QuizIssueCodes.ImageTooLarge, at, ("src", src), ("maxMb", ImageLimits.MaxBytes / MegaByte)));
            }
            else if (Readable(file.Bytes) is not var (contentType, bytes))
            {
                issues.Add(QuizIssue.Of(QuizIssueCodes.ImageType, at, ("src", src)));
            }
            else
            {
                images[src] = new AttachedImage(src, Convert.ToHexStringLower(SHA256.HashData(bytes)), contentType, bytes);
            }
        }

        if (images.Values.DistinctBy(i => i.Hash).Sum(i => (long)i.Bytes.Length) > ImageLimits.MaxTotalBytes)
        {
            issues.Add(QuizIssue.Of(QuizIssueCodes.ImagesTooLarge, "", ("maxMb", ImageLimits.MaxTotalBytes / MegaByte)));
        }

        return (images.Values.ToList(), issues);
    }

    private (string ContentType, byte[] Bytes)? Readable(byte[] bytes) =>
        ImageFormats.Detect(bytes) is { } raster ? (raster, bytes)
        : svg.Sanitize(bytes) is { } clean ? (ImageFormats.Svg, clean)
        : null;

    /// <summary>Links the pictures to a new quiz version; the caller saves.</summary>
    public async Task AttachToQuizAsync(Guid familyId, Guid storedQuizId, IReadOnlyList<AttachedImage> images)
    {
        foreach (var image in images)
        {
            store.Add(DocumentImage.ForQuiz(familyId, storedQuizId, image.Src, image.Hash));
        }

        await StoreAsync(images);
    }

    /// <summary>Links the pictures to a lesson in place of the ones it had; the caller saves.</summary>
    public async Task AttachToLessonAsync(Guid familyId, Guid storedLessonId, IReadOnlyList<AttachedImage> images)
    {
        foreach (var old in await store.ForLessonAsync(storedLessonId))
        {
            store.Remove(old);
        }

        foreach (var image in images)
        {
            store.Add(DocumentImage.ForLesson(familyId, storedLessonId, image.Src, image.Hash));
        }

        await StoreAsync(images);
    }

    /// <summary>After a quiz, lesson or family was deleted: drops the bytes nobody uses any more.</summary>
    public Task RemoveUnusedAsync() => store.RemoveUnusedAsync();

    /// <summary>Adds the bytes of pictures the server does not have yet; the caller saves.</summary>
    private async Task StoreAsync(IReadOnlyList<AttachedImage> images)
    {
        foreach (var image in images.DistinctBy(i => i.Hash))
        {
            if (!await store.ExistsAsync(image.Hash))
            {
                store.Add(new StoredImage(image.Hash, image.ContentType, image.Bytes, time.GetUtcNow()));
            }
        }
    }

    public async Task<IReadOnlyDictionary<string, string>> ForQuizAsync(Guid storedQuizId, bool anyFamily = false) =>
        Links(anyFamily ? await store.ForQuizAnyFamilyAsync(storedQuizId) : await store.ForQuizAsync(storedQuizId));

    public async Task<IReadOnlyDictionary<string, string>> ForLessonAsync(Guid storedLessonId, bool anyFamily = false) =>
        Links(anyFamily ? await store.ForLessonAnyFamilyAsync(storedLessonId) : await store.ForLessonAsync(storedLessonId));

    /// <summary>The pictures as files named by their <c>src</c>, to download or copy a quiz or lesson.</summary>
    private async Task<IReadOnlyList<UploadedFile>> FilesAsync(IReadOnlyList<DocumentImage> images)
    {
        var files = new List<UploadedFile>();
        foreach (var image in images)
        {
            if (await store.FindAsync(image.Hash) is { } stored)
            {
                files.Add(new UploadedFile(image.Name, stored.Bytes));
            }
        }

        return files;
    }

    public async Task<IReadOnlyList<UploadedFile>> QuizFilesAsync(Guid storedQuizId, bool anyFamily = false) =>
        await FilesAsync(anyFamily ? await store.ForQuizAnyFamilyAsync(storedQuizId) : await store.ForQuizAsync(storedQuizId));

    public async Task<IReadOnlyList<UploadedFile>> LessonFilesAsync(Guid storedLessonId, bool anyFamily = false) =>
        await FilesAsync(anyFamily ? await store.ForLessonAnyFamilyAsync(storedLessonId) : await store.ForLessonAsync(storedLessonId));

    /// <summary>A picture for the logged-in parent or child, if a quiz or lesson they may see uses it.</summary>
    public async Task<Result<StoredImage>> ImageAsync(string hash)
    {
        if (hash.Length != 64 || !hash.All(Uri.IsHexDigit) || hash.Any(char.IsUpper) || !await store.IsVisibleAsync(hash))
        {
            return AppError.NotFound();
        }

        return await store.FindAsync(hash) is { } image ? image : AppError.NotFound();
    }

    private static Dictionary<string, string> Links(IReadOnlyList<DocumentImage> images) =>
        images.GroupBy(i => i.Name).ToDictionary(g => g.Key, g => g.First().Hash);

    private static string Normalize(string path)
    {
        path = path.Replace('\\', '/').TrimStart('/');
        return path.StartsWith("./", StringComparison.Ordinal) ? path[2..] : path;
    }

    private static string FileName(string path) => Normalize(path).Split('/')[^1];
}
