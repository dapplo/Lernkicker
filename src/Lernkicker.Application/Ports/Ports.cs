using Lernkicker.Domain.Families;
using Lernkicker.Domain.Library;
using Lernkicker.Domain.Practice;
using Lernkicker.Domain.Quizzes;

namespace Lernkicker.Application.Ports;

// Driven ports: what the use cases need from the outside world. The adapters (Persistence, QuizFormat, Web) implement them.

/// <summary>
/// The family of whoever is logged in. The persistence adapter limits every family-owned query to it, as a
/// second line of defence behind the explicit checks in the use cases.
/// </summary>
public interface IFamilyScope
{
    Guid? FamilyId { get; }

    /// <summary>
    /// For callers that are authenticated another way than a login: a calendar feed token, a background job.
    /// Fails when a different family is already logged in.
    /// </summary>
    void Enter(Guid familyId);
}

/// <summary>Reads and writes the quiz/v1 JSON format.</summary>
public interface IQuizFormat
{
    /// <summary>Parses and fully validates an uploaded file (schema and <see cref="QuizRules"/>).</summary>
    QuizParseResult Parse(string json);

    /// <summary>Reads a file that was validated on upload.</summary>
    Quiz Read(string storedSource);
}

public sealed record QuizParseResult(Quiz? Quiz, IReadOnlyList<QuizIssue> Issues)
{
    public bool IsValid => Quiz is not null && Issues.Count == 0;
}

/// <summary>Hashes children's PINs (slow hash, like passwords).</summary>
public interface IPinHasher
{
    string Hash(string pin);

    bool Verify(string hash, string pin);
}

/// <summary>Parent accounts with login credentials, kept by the identity adapter.</summary>
public interface IParentAccounts
{
    /// <summary>Fails with codes starting with <see cref="ErrorCodes.AccountPrefix"/> (e.g. duplicate e-mail, weak password).</summary>
    Task<Result<Guid>> CreateAsync(NewParent parent);

    Task<ParentAccount?> FindAsync(Guid id);

    Task SetLanguageAsync(Guid id, string language);
}

public sealed record NewParent(string Email, string Password, string DisplayName, Guid FamilyId, string Language);

public sealed record ParentAccount(Guid Id, string Email, string DisplayName, Guid FamilyId, string Language, bool IsAdmin);

public interface IFamilyRepository
{
    void Add(Family family);

    Task<Family?> FindAsync(Guid id);

    /// <summary>All families with their member counts (admin only, across families).</summary>
    Task<IReadOnlyList<FamilySummary>> ListSummariesAsync();

    /// <summary>Deletes the family with all parents, children, quizzes and results.</summary>
    Task<bool> DeleteAsync(Guid id);

    void Add(Invitation invitation);

    Task<Invitation?> FindInvitationAsync(string codeHash);
}

public interface IChildRepository
{
    void Add(Child child);

    Task<Child?> FindAsync(Guid id);

    /// <summary>Looks up a child outside the logged-in family, for pairing and the child's PIN login.</summary>
    Task<Child?> FindAnyFamilyAsync(Guid id);

    Task<IReadOnlyList<ChildSummary>> ListSummariesAsync();

    Task<bool> DeleteAsync(Guid id);

    void Add(PairingCode code);

    void Remove(PairingCode code);

    Task<PairingCode?> FindPairingCodeAnyFamilyAsync(string codeHash);

    void Add(ChildDevice device);

    void Remove(ChildDevice device);

    Task<ChildDevice?> FindDeviceAsync(Guid childId, Guid deviceId);

    Task<IReadOnlyList<ChildDevice>> ListDevicesAsync(Guid childId);

    /// <summary>The device behind a device cookie; the child is not logged in yet, so across families.</summary>
    Task<ChildDevice?> FindDeviceByTokenAnyFamilyAsync(string tokenHash);

    /// <summary>False once a parent removed or the PIN check locked the device.</summary>
    Task<bool> IsDeviceActiveAnyFamilyAsync(Guid deviceId);
}

public interface IQuizLibrary
{
    void Add(StoredQuiz quiz);

    void Remove(StoredQuiz quiz);

    /// <summary>The newest version of every quiz that is not archived.</summary>
    Task<IReadOnlyList<StoredQuiz>> ListCurrentAsync();

    Task<StoredQuiz?> FindCurrentAsync(string quizId);

    Task<StoredQuiz?> FindAsync(Guid storedQuizId);

    Task<IReadOnlyList<StoredQuiz>> VersionsAsync(string quizId);
}

public interface IPracticeRepository
{
    void Add(Assignment assignment);

    void Remove(Assignment assignment);

    Task<Assignment?> FindAssignmentAsync(Guid childId, Guid assignmentId);

    Task<Assignment?> FindAssignmentForQuizAsync(Guid childId, Guid storedQuizId);

    Task<IReadOnlyList<Assignment>> AssignmentsForQuizzesAsync(IReadOnlyCollection<Guid> storedQuizIds);

    Task<IReadOnlyList<AssignmentSummary>> ListAssignmentSummariesAsync(Guid childId);

    void Add(Attempt attempt);

    Task<Attempt?> FindAttemptAsync(Guid childId, Guid attemptId);

    Task<bool> AnyAttemptsAsync(IReadOnlyCollection<Guid> storedQuizIds);

    /// <summary>The newest attempts first.</summary>
    Task<IReadOnlyList<AttemptSummary>> ListAttemptSummariesAsync(Guid childId, int max);
}

/// <summary>Saves all changes of a use case at once.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync();

    Task<IUnitOfWorkTransaction> BeginTransactionAsync();
}

public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync();
}
