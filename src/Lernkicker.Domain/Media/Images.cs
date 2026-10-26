using Lernkicker.Domain.Families;

namespace Lernkicker.Domain.Media;

/// <summary>
/// A picture in a quiz or lesson. <c>Src</c> names a file uploaded together with the JSON (relative to it,
/// e.g. <c>plants/rose.jpg</c>); <c>Alt</c> describes the picture for screen readers and when it cannot be shown.
/// </summary>
public sealed record ImageRef(string Src, string Alt, string? Caption = null, string? Credit = null);

/// <summary>A picture named in a quiz or lesson, with the JSON path of its <c>image</c> object.</summary>
public sealed record ImageUse(string Path, ImageRef Image);

public static class ImageLimits
{
    /// <summary>Largest single picture.</summary>
    public const int MaxBytes = 2 * 1024 * 1024;

    /// <summary>All pictures of one quiz or lesson together.</summary>
    public const int MaxTotalBytes = 20 * 1024 * 1024;

    /// <summary>Files in one upload, the JSON included.</summary>
    public const int MaxFiles = 100;
}

/// <summary>
/// The picture formats Lernkicker accepts, recognised by their content, never by the file name. SVG is accepted
/// only after cleaning (it can carry scripts and links), so <see cref="Detect"/> covers the raster formats.
/// </summary>
public static class ImageFormats
{
    public const string Png = "image/png";
    public const string Jpeg = "image/jpeg";
    public const string Gif = "image/gif";
    public const string WebP = "image/webp";
    public const string Svg = "image/svg+xml";

    /// <summary>The media type of a PNG, JPEG, GIF or WebP file, or null for anything else.</summary>
    public static string? Detect(ReadOnlySpan<byte> bytes) => bytes switch
    {
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..] => Png,
        [0xFF, 0xD8, 0xFF, ..] => Jpeg,
        [0x47, 0x49, 0x46, 0x38, 0x37 or 0x39, 0x61, ..] => Gif,
        [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50, ..] => WebP,
        _ => null,
    };

    public static string Extension(string contentType) => contentType switch
    {
        Png => ".png",
        Jpeg => ".jpg",
        Gif => ".gif",
        WebP => ".webp",
        Svg => ".svg",
        _ => "",
    };
}

/// <summary>The bytes of one picture, stored once however many quizzes, lessons or families use it.</summary>
public sealed class StoredImage
{
    /// <summary>Lower-case hex SHA-256 of <see cref="Bytes"/>; also the picture's address in URLs.</summary>
    public string Hash { get; private set; }

    public string ContentType { get; private set; }
    public int Length { get; private set; }
    public byte[] Bytes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public StoredImage(string hash, string contentType, byte[] bytes, DateTimeOffset createdAt)
    {
        Hash = hash;
        ContentType = contentType;
        Bytes = bytes;
        Length = bytes.Length;
        CreatedAt = createdAt;
    }

    // For the persistence adapter
    private StoredImage()
    {
        Hash = ContentType = "";
        Bytes = [];
    }
}

/// <summary>
/// Which picture a <c>src</c> of a stored quiz version or lesson stands for. Exactly one of
/// <see cref="StoredQuizId"/> and <see cref="StoredLessonId"/> is set; deleting the quiz or lesson deletes it.
/// </summary>
public sealed class DocumentImage : IFamilyOwned
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid FamilyId { get; private set; }
    public Guid? StoredQuizId { get; private set; }
    public Guid? StoredLessonId { get; private set; }

    /// <summary>The <c>src</c> exactly as in the JSON.</summary>
    public string Name { get; private set; }

    public string Hash { get; private set; }

    private DocumentImage(Guid familyId, Guid? storedQuizId, Guid? storedLessonId, string name, string hash)
    {
        FamilyId = familyId;
        StoredQuizId = storedQuizId;
        StoredLessonId = storedLessonId;
        Name = name;
        Hash = hash;
    }

    public static DocumentImage ForQuiz(Guid familyId, Guid storedQuizId, string name, string hash) => new(familyId, storedQuizId, null, name, hash);

    public static DocumentImage ForLesson(Guid familyId, Guid storedLessonId, string name, string hash) => new(familyId, null, storedLessonId, name, hash);
}
