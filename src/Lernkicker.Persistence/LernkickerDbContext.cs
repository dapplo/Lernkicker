using System.Text.Json;
using Lernkicker.Application.Ports;
using Lernkicker.Domain.Calendar;
using Lernkicker.Domain.Families;
using Lernkicker.Domain.Lessons;
using Lernkicker.Domain.Library;
using Lernkicker.Domain.Messaging;
using Lernkicker.Domain.Practice;
using Lernkicker.Domain.Quizzes;
using Lernkicker.Domain.Settings;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore;

namespace Lernkicker.Persistence;

/// <summary>A parent's login. The admin is a parent in their own family with the admin role.</summary>
public sealed class ParentUser : IdentityUser<Guid>
{
    public Guid FamilyId { get; set; }
    public required string DisplayName { get; set; }
    public string Language { get; set; } = "de";
}

public sealed class LernkickerDbContext(DbContextOptions<LernkickerDbContext> options, IFamilyScope familyScope)
    : IdentityDbContext<ParentUser, IdentityRole<Guid>, Guid>(options)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // Read by the query filters on every query; null (not logged in) matches no family at all.
    private Guid? FamilyId => familyScope.FamilyId;

    public DbSet<Family> Families => Set<Family>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<Child> Children => Set<Child>();
    public DbSet<ChildDevice> ChildDevices => Set<ChildDevice>();
    public DbSet<PairingCode> PairingCodes => Set<PairingCode>();
    public DbSet<StoredQuiz> Quizzes => Set<StoredQuiz>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<Attempt> Attempts => Set<Attempt>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<CalendarSubscription> CalendarSubscriptions => Set<CalendarSubscription>();
    public DbSet<CalendarFeed> CalendarFeeds => Set<CalendarFeed>();
    public DbSet<StoredLesson> Lessons => Set<StoredLesson>();
    public DbSet<LessonAssignment> LessonAssignments => Set<LessonAssignment>();
    public DbSet<TelegramLink> TelegramLinks => Set<TelegramLink>();
    public DbSet<TelegramLinkCode> TelegramLinkCodes => Set<TelegramLinkCode>();
    public DbSet<ServerSecret> ServerSecrets => Set<ServerSecret>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ParentUser>(e =>
        {
            e.Property(x => x.DisplayName).HasMaxLength(100);
            e.Property(x => x.Language).HasMaxLength(10);
            e.HasOne<Family>().WithMany().HasForeignKey(x => x.FamilyId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Family>(e => e.Property(x => x.Name).HasMaxLength(Family.MaxNameLength));

        builder.Entity<Invitation>(e =>
        {
            e.HasIndex(x => x.CodeHash).IsUnique();
            e.Property(x => x.NewFamilyName).HasMaxLength(Family.MaxNameLength);
            e.HasOne<Family>().WithMany().HasForeignKey(x => x.FamilyId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Child>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(Child.MaxNameLength);
            e.HasOne<Family>().WithMany().HasForeignKey(x => x.FamilyId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.FamilyId == FamilyId);
        });

        builder.Entity<ChildDevice>(e =>
        {
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.Property(x => x.Label).HasMaxLength(ChildDevice.MaxLabelLength);
            e.HasOne<Child>().WithMany().HasForeignKey(x => x.ChildId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.FamilyId == FamilyId);
        });

        builder.Entity<PairingCode>(e =>
        {
            e.HasIndex(x => x.CodeHash).IsUnique();
            e.HasOne<Child>().WithMany().HasForeignKey(x => x.ChildId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.FamilyId == FamilyId);
        });

        builder.Entity<StoredQuiz>(e =>
        {
            e.HasIndex(x => new { x.FamilyId, x.QuizId, x.Version }).IsUnique();
            e.Property(x => x.QuizId).HasMaxLength(64);
            e.Property(x => x.Title).HasMaxLength(120);
            e.Property(x => x.Subject).HasMaxLength(20);
            e.Property(x => x.Topic).HasMaxLength(120);
            e.HasOne<Family>().WithMany().HasForeignKey(x => x.FamilyId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.FamilyId == FamilyId);
        });

        builder.Entity<Assignment>(e =>
        {
            e.HasIndex(x => new { x.ChildId, x.StoredQuizId }).IsUnique();
            e.HasOne<Child>().WithMany().HasForeignKey(x => x.ChildId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<StoredQuiz>().WithMany().HasForeignKey(x => x.StoredQuizId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.FamilyId == FamilyId);
        });

        builder.Entity<Attempt>(e =>
        {
            e.HasIndex(x => new { x.ChildId, x.SubmittedAt });
            e.HasOne<Child>().WithMany().HasForeignKey(x => x.ChildId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<StoredQuiz>().WithMany().HasForeignKey(x => x.StoredQuizId).OnDelete(DeleteBehavior.Cascade);
            // Answers and result are only ever read as a whole, so they are stored as JSON
            // Attempts never change, so comparing by reference is enough for change tracking
            e.Property(x => x.Answers).HasConversion(
                v => JsonSerializer.Serialize(v, Json),
                v => JsonSerializer.Deserialize<Dictionary<string, ExerciseAnswer>>(v, Json)!,
                new ValueComparer<IReadOnlyDictionary<string, ExerciseAnswer>>((a, b) => ReferenceEquals(a, b), v => v.GetHashCode(), v => v));
            e.Property(x => x.Result).HasConversion(
                v => JsonSerializer.Serialize(v, Json),
                v => JsonSerializer.Deserialize<QuizResult>(v, Json)!);
            e.HasQueryFilter(x => x.FamilyId == FamilyId);
        });

        builder.Entity<CalendarEvent>(e =>
        {
            e.HasIndex(x => new { x.ChildId, x.Date });
            e.HasIndex(x => x.SubscriptionId);
            e.Property(x => x.Kind).HasMaxLength(20);
            e.Property(x => x.Title).HasMaxLength(CalendarEvent.MaxTitleLength);
            e.Property(x => x.Location).HasMaxLength(CalendarEvent.MaxLocationLength);
            e.Property(x => x.ExternalId).HasMaxLength(500);
            e.HasOne<Child>().WithMany().HasForeignKey(x => x.ChildId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<CalendarSubscription>().WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.FamilyId == FamilyId);
        });

        builder.Entity<CalendarSubscription>(e =>
        {
            e.Property(x => x.Label).HasMaxLength(CalendarSubscription.MaxLabelLength);
            e.Property(x => x.Url).HasMaxLength(CalendarSubscription.MaxUrlLength);
            e.Property(x => x.Kind).HasMaxLength(20);
            e.Property(x => x.LastError).HasMaxLength(50);
            e.HasOne<Child>().WithMany().HasForeignKey(x => x.ChildId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.FamilyId == FamilyId);
        });

        builder.Entity<CalendarFeed>(e =>
        {
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasOne<Child>().WithMany().HasForeignKey(x => x.ChildId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.FamilyId == FamilyId);
        });

        builder.Entity<StoredLesson>(e =>
        {
            e.HasIndex(x => new { x.FamilyId, x.LessonId }).IsUnique();
            e.Property(x => x.LessonId).HasMaxLength(64);
            e.Property(x => x.Title).HasMaxLength(120);
            e.Property(x => x.Subject).HasMaxLength(20);
            e.Property(x => x.Topic).HasMaxLength(120);
            e.HasOne<Family>().WithMany().HasForeignKey(x => x.FamilyId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.FamilyId == FamilyId);
        });

        builder.Entity<LessonAssignment>(e =>
        {
            e.HasIndex(x => new { x.ChildId, x.StoredLessonId }).IsUnique();
            e.HasOne<Child>().WithMany().HasForeignKey(x => x.ChildId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<StoredLesson>().WithMany().HasForeignKey(x => x.StoredLessonId).OnDelete(DeleteBehavior.Cascade);
            // A short list of block ids, stored as JSON
            e.Property(x => x.KnownBlocks).HasConversion(
                v => JsonSerializer.Serialize(v, Json),
                v => JsonSerializer.Deserialize<List<string>>(v, Json)!,
                new ValueComparer<IReadOnlyList<string>>((a, b) => a!.SequenceEqual(b!), v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s)), v => v.ToList()));
            e.HasQueryFilter(x => x.FamilyId == FamilyId);
        });

        builder.Entity<TelegramLink>(e =>
        {
            e.HasIndex(x => x.ParentId).IsUnique();
            e.HasIndex(x => x.ChatId).IsUnique();
            e.HasOne<ParentUser>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.FamilyId == FamilyId);
        });

        builder.Entity<TelegramLinkCode>(e =>
        {
            e.HasIndex(x => x.CodeHash).IsUnique();
            e.Property(x => x.CodeHash).HasMaxLength(64);
            e.HasOne<ParentUser>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.FamilyId == FamilyId);
        });

        // Not owned by a family: only the admin endpoints and the bot read them
        builder.Entity<ServerSecret>(e =>
        {
            e.HasKey(x => x.Name);
            e.Property(x => x.Name).HasMaxLength(64);
        });

        // SQLite cannot order or compare DateTimeOffset; store as UTC ticks.
        foreach (var entity in builder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties().Where(p => p.ClrType == typeof(DateTimeOffset) || p.ClrType == typeof(DateTimeOffset?)))
            {
                property.SetValueConverter(new DateTimeOffsetToBinaryConverter());
            }
        }
    }
}
