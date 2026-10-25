using System.Text.Json;
using Lernkicker.Application.Ports;
using Lernkicker.Domain.Families;
using Lernkicker.Domain.Library;
using Lernkicker.Domain.Practice;
using Lernkicker.Domain.Quizzes;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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
