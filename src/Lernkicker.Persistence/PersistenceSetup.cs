using Lernkicker.Application.Ports;
using Lernkicker.Domain.Families;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Lernkicker.Persistence;

/// <summary>Settings for the first admin, e.g. from the environment variables Admin__Email and Admin__Password.</summary>
public sealed class AdminOptions
{
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string Name { get; set; } = "Admin";
    public string Family { get; set; } = "My family";
    public string Language { get; set; } = "de";
}

public static class PersistenceSetup
{
    /// <summary>Registers the database and the repositories. Identity itself is wired up by the host.</summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<LernkickerDbContext>(o => o.UseSqlite(connectionString));
        services.AddScoped<IFamilyRepository, FamilyRepository>();
        services.AddScoped<IChildRepository, ChildRepository>();
        services.AddScoped<IQuizLibrary, QuizLibrary>();
        services.AddScoped<IPracticeRepository, PracticeRepository>();
        services.AddScoped<ICalendarRepository, CalendarRepository>();
        services.AddScoped<ILessonRepository, LessonRepository>();
        services.AddScoped<ITelegramRepository, TelegramRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IParentAccounts, ParentAccounts>();
        services.AddSingleton<IPinHasher, PinHasher>();
        return services;
    }

    /// <summary>Applies migrations and creates the admin (with their own family) if no admin exists yet.</summary>
    public static async Task InitializeAsync(IServiceProvider services, AdminOptions admin, ILogger logger)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LernkickerDbContext>();
        await db.Database.MigrateAsync();

        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roles.RoleExistsAsync(Roles.Admin))
        {
            await roles.CreateAsync(new IdentityRole<Guid>(Roles.Admin));
        }

        var users = scope.ServiceProvider.GetRequiredService<UserManager<ParentUser>>();
        if ((await users.GetUsersInRoleAsync(Roles.Admin)).Count > 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(admin.Email) || string.IsNullOrWhiteSpace(admin.Password))
        {
            logger.LogWarning("No admin exists yet. Set Admin__Email and Admin__Password to create one.");
            return;
        }

        var time = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var family = new Family(admin.Family, time.GetUtcNow());
        db.Families.Add(family);
        await db.SaveChangesAsync();

        var user = new ParentUser { UserName = admin.Email, Email = admin.Email, DisplayName = admin.Name, FamilyId = family.Id, Language = admin.Language };
        var created = await users.CreateAsync(user, admin.Password);
        if (!created.Succeeded)
        {
            db.Families.Remove(family);
            await db.SaveChangesAsync();
            throw new InvalidOperationException("Creating the admin failed: " + string.Join(" ", created.Errors.Select(e => e.Description)));
        }

        await users.AddToRoleAsync(user, Roles.Admin);
        logger.LogInformation("Created admin {Email}.", admin.Email);
    }
}
