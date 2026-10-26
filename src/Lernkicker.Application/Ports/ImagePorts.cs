using Lernkicker.Domain.Media;

namespace Lernkicker.Application.Ports;

/// <summary>
/// Pictures of quizzes and lessons: the bytes once per hash (not owned by a family), and per stored quiz version
/// or lesson which <c>src</c> means which hash (filtered by family like everything else).
/// </summary>
public interface IImageStore
{
    Task<bool> ExistsAsync(string hash);
    void Add(StoredImage image);
    Task<StoredImage?> FindAsync(string hash);

    void Add(DocumentImage image);
    void Remove(DocumentImage image);
    Task<IReadOnlyList<DocumentImage>> ForQuizAsync(Guid storedQuizId);
    Task<IReadOnlyList<DocumentImage>> ForLessonAsync(Guid storedLessonId);

    /// <summary>The pictures of another family's quiz or lesson; only call it for one the shared library returned.</summary>
    Task<IReadOnlyList<DocumentImage>> ForQuizAnyFamilyAsync(Guid storedQuizId);

    /// <inheritdoc cref="ForQuizAnyFamilyAsync"/>
    Task<IReadOnlyList<DocumentImage>> ForLessonAnyFamilyAsync(Guid storedLessonId);

    /// <summary>True when the logged-in family uses the picture, or a current quiz or lesson in the shared library does.</summary>
    Task<bool> IsVisibleAsync(string hash);

    /// <summary>Deletes the bytes of pictures no quiz or lesson of any family uses any more.</summary>
    Task RemoveUnusedAsync();
}

/// <summary>Makes an uploaded SVG safe to show: no scripts, no event handlers, nothing loaded from elsewhere.</summary>
public interface ISvgSanitizer
{
    /// <summary>The cleaned SVG, or null when the bytes are not an SVG document that can be read safely.</summary>
    byte[]? Sanitize(byte[] bytes);
}
