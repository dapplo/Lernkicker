using Lernkicker.Application;
using Lernkicker.Application.Ports;
using Lernkicker.Domain.Calendar;
using Lernkicker.Domain.Families;
using Lernkicker.Domain.Library;
using Lernkicker.Domain.Practice;
using Microsoft.EntityFrameworkCore;

namespace Lernkicker.Persistence;

// Methods ending in AnyFamily bypass the family filter on purpose; everything else only sees the logged-in family.

internal sealed class FamilyRepository(LernkickerDbContext db) : IFamilyRepository
{
    public void Add(Family family) => db.Families.Add(family);

    public Task<Family?> FindAsync(Guid id) => db.Families.SingleOrDefaultAsync(f => f.Id == id);

    public async Task<IReadOnlyList<FamilySummary>> ListSummariesAsync()
    {
        var parents = await db.Users.GroupBy(u => u.FamilyId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        var children = await db.Children.IgnoreQueryFilters().GroupBy(c => c.FamilyId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        var families = await db.Families.OrderBy(f => f.Name).ToListAsync();
        return families.Select(f => new FamilySummary(f.Id, f.Name, parents.GetValueOrDefault(f.Id), children.GetValueOrDefault(f.Id), f.CreatedAt)).ToList();
    }

    // The database cascades to parents, children, devices, quizzes, assignments and attempts
    public async Task<bool> DeleteAsync(Guid id) => await db.Families.Where(f => f.Id == id).ExecuteDeleteAsync() > 0;

    public void Add(Invitation invitation) => db.Invitations.Add(invitation);

    public Task<Invitation?> FindInvitationAsync(string codeHash) => db.Invitations.SingleOrDefaultAsync(i => i.CodeHash == codeHash);
}

internal sealed class ChildRepository(LernkickerDbContext db) : IChildRepository
{
    public void Add(Child child) => db.Children.Add(child);

    public Task<Child?> FindAsync(Guid id) => db.Children.SingleOrDefaultAsync(c => c.Id == id);

    public Task<Child?> FindAnyFamilyAsync(Guid id) => db.Children.IgnoreQueryFilters().SingleOrDefaultAsync(c => c.Id == id);

    public async Task<IReadOnlyList<ChildSummary>> ListSummariesAsync()
    {
        var devices = await db.ChildDevices.GroupBy(d => d.ChildId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        var children = await db.Children.OrderBy(c => c.Name).ToListAsync();
        return children.Select(c => new ChildSummary(c.Id, c.Name, c.GradeLevel, c.PinHash is not null, devices.GetValueOrDefault(c.Id))).ToList();
    }

    public async Task<bool> DeleteAsync(Guid id) => await db.Children.Where(c => c.Id == id).ExecuteDeleteAsync() > 0;

    public void Add(PairingCode code) => db.PairingCodes.Add(code);

    public void Remove(PairingCode code) => db.PairingCodes.Remove(code);

    public Task<PairingCode?> FindPairingCodeAnyFamilyAsync(string codeHash) =>
        db.PairingCodes.IgnoreQueryFilters().SingleOrDefaultAsync(c => c.CodeHash == codeHash);

    public void Add(ChildDevice device) => db.ChildDevices.Add(device);

    public void Remove(ChildDevice device) => db.ChildDevices.Remove(device);

    public Task<ChildDevice?> FindDeviceAsync(Guid childId, Guid deviceId) =>
        db.ChildDevices.SingleOrDefaultAsync(d => d.ChildId == childId && d.Id == deviceId);

    public async Task<IReadOnlyList<ChildDevice>> ListDevicesAsync(Guid childId) =>
        await db.ChildDevices.Where(d => d.ChildId == childId).ToListAsync();

    public Task<ChildDevice?> FindDeviceByTokenAnyFamilyAsync(string tokenHash) =>
        db.ChildDevices.IgnoreQueryFilters().SingleOrDefaultAsync(d => d.TokenHash == tokenHash);

    public Task<bool> IsDeviceActiveAnyFamilyAsync(Guid deviceId) =>
        db.ChildDevices.IgnoreQueryFilters().AnyAsync(d => d.Id == deviceId && !d.Locked);
}

internal sealed class QuizLibrary(LernkickerDbContext db) : IQuizLibrary
{
    public void Add(StoredQuiz quiz) => db.Quizzes.Add(quiz);

    public void Remove(StoredQuiz quiz) => db.Quizzes.Remove(quiz);

    public async Task<IReadOnlyList<StoredQuiz>> ListCurrentAsync() =>
        (await db.Quizzes.Where(q => !q.Archived).ToListAsync())
            .GroupBy(q => q.QuizId)
            .Select(g => g.MaxBy(q => q.Version)!)
            .ToList();

    public Task<StoredQuiz?> FindCurrentAsync(string quizId) =>
        db.Quizzes.Where(q => q.QuizId == quizId && !q.Archived).OrderByDescending(q => q.Version).FirstOrDefaultAsync();

    public Task<StoredQuiz?> FindAsync(Guid storedQuizId) => db.Quizzes.SingleOrDefaultAsync(q => q.Id == storedQuizId);

    public async Task<IReadOnlyList<StoredQuiz>> VersionsAsync(string quizId) =>
        await db.Quizzes.Where(q => q.QuizId == quizId).ToListAsync();
}

internal sealed class PracticeRepository(LernkickerDbContext db) : IPracticeRepository
{
    public void Add(Assignment assignment) => db.Assignments.Add(assignment);

    public void Remove(Assignment assignment) => db.Assignments.Remove(assignment);

    public Task<Assignment?> FindAssignmentAsync(Guid childId, Guid assignmentId) =>
        db.Assignments.SingleOrDefaultAsync(a => a.ChildId == childId && a.Id == assignmentId);

    public Task<Assignment?> FindAssignmentForQuizAsync(Guid childId, Guid storedQuizId) =>
        db.Assignments.SingleOrDefaultAsync(a => a.ChildId == childId && a.StoredQuizId == storedQuizId);

    public async Task<IReadOnlyList<Assignment>> AssignmentsForQuizzesAsync(IReadOnlyCollection<Guid> storedQuizIds) =>
        await db.Assignments.Where(a => storedQuizIds.Contains(a.StoredQuizId)).ToListAsync();

    public async Task<IReadOnlyList<AssignmentSummary>> ListAssignmentSummariesAsync(Guid childId) =>
        await (from a in db.Assignments
               join q in db.Quizzes on a.StoredQuizId equals q.Id
               where a.ChildId == childId
               select new AssignmentSummary(a.Id, q.Id, q.QuizId, q.Version, q.Title, q.Subject, q.Topic, q.MaxPoints, a.DueOn, a.AssignedAt))
            .ToListAsync();

    public void Add(Attempt attempt) => db.Attempts.Add(attempt);

    public Task<Attempt?> FindAttemptAsync(Guid childId, Guid attemptId) =>
        db.Attempts.SingleOrDefaultAsync(a => a.ChildId == childId && a.Id == attemptId);

    public Task<bool> AnyAttemptsAsync(IReadOnlyCollection<Guid> storedQuizIds) =>
        db.Attempts.AnyAsync(a => storedQuizIds.Contains(a.StoredQuizId));

    public async Task<IReadOnlyList<AttemptSummary>> ListAttemptSummariesAsync(Guid childId, int max) =>
        await (from a in db.Attempts
               join q in db.Quizzes on a.StoredQuizId equals q.Id
               where a.ChildId == childId
               orderby a.SubmittedAt descending
               select new AttemptSummary(a.Id, q.Id, q.QuizId, q.Title, q.Subject, a.Channel, a.SubmittedAt, a.Points, a.MaxPoints, a.Percent, a.Mark))
            .Take(max)
            .ToListAsync();
}

internal sealed class CalendarRepository(LernkickerDbContext db) : ICalendarRepository
{
    public void Add(CalendarEvent calendarEvent) => db.CalendarEvents.Add(calendarEvent);

    public void Remove(CalendarEvent calendarEvent) => db.CalendarEvents.Remove(calendarEvent);

    public Task<CalendarEvent?> FindEventAsync(Guid childId, Guid eventId) =>
        db.CalendarEvents.SingleOrDefaultAsync(e => e.ChildId == childId && e.Id == eventId);

    public async Task<IReadOnlyList<CalendarEvent>> ListEventsAsync(Guid childId, DateOnly from, DateOnly to) =>
        await db.CalendarEvents.Where(e => e.ChildId == childId && e.Date >= from && e.Date <= to).ToListAsync();

    public Task RemoveImportedAsync(Guid subscriptionId) =>
        db.CalendarEvents.Where(e => e.SubscriptionId == subscriptionId).ExecuteDeleteAsync();

    public void Add(CalendarSubscription subscription) => db.CalendarSubscriptions.Add(subscription);

    public void Remove(CalendarSubscription subscription) => db.CalendarSubscriptions.Remove(subscription);

    public Task<CalendarSubscription?> FindSubscriptionAsync(Guid childId, Guid subscriptionId) =>
        db.CalendarSubscriptions.SingleOrDefaultAsync(s => s.ChildId == childId && s.Id == subscriptionId);

    public Task<CalendarSubscription?> FindSubscriptionAsync(Guid subscriptionId) =>
        db.CalendarSubscriptions.SingleOrDefaultAsync(s => s.Id == subscriptionId);

    public async Task<IReadOnlyList<CalendarSubscription>> ListSubscriptionsAsync(Guid childId) =>
        await db.CalendarSubscriptions.Where(s => s.ChildId == childId).ToListAsync();

    public async Task<IReadOnlyList<SubscriptionRef>> ListAllSubscriptionsAnyFamilyAsync() =>
        await db.CalendarSubscriptions.IgnoreQueryFilters().Select(s => new SubscriptionRef(s.Id, s.FamilyId)).ToListAsync();

    public void Add(CalendarFeed feed) => db.CalendarFeeds.Add(feed);

    public Task RemoveFeedsAsync(Guid childId) => db.CalendarFeeds.Where(f => f.ChildId == childId).ExecuteDeleteAsync();

    public Task<CalendarFeed?> FindFeedAnyFamilyAsync(string tokenHash) =>
        db.CalendarFeeds.IgnoreQueryFilters().SingleOrDefaultAsync(f => f.TokenHash == tokenHash);
}

internal sealed class UnitOfWork(LernkickerDbContext db) : IUnitOfWork
{
    public Task SaveChangesAsync() => db.SaveChangesAsync();

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync() => new Transaction(await db.Database.BeginTransactionAsync());

    private sealed class Transaction(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction inner) : IUnitOfWorkTransaction
    {
        public Task CommitAsync() => inner.CommitAsync();

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
