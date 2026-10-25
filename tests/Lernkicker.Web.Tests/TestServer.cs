using System.Net;
using System.Net.Http.Json;
using Lernkicker.Contracts;
using Lernkicker.Domain.Quizzes;
using System.Collections.Concurrent;
using Lernkicker.Application;
using Lernkicker.Application.Ports;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Lernkicker.Web.Tests;

/// <summary>A real server with its own SQLite file and a seeded admin.</summary>
public sealed class LernkickerFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@example.org";
    public const string AdminPassword = "admin-password-123";

    public int LimitPerMinute { get; init; } = 10_000;

    /// <summary>Lernkicker:PublicUrl, if set.</summary>
    public string? PublicUrl { get; init; }

    /// <summary>What the subscribed calendars "on the internet" return.</summary>
    public FakeCalendarSource Calendars { get; } = new();

    private readonly string _dataPath = Path.Combine(Path.GetTempPath(), "lernkicker-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // Static web assets (the client app) are only wired up automatically in Development
        builder.UseStaticWebAssets();
        builder.UseSetting("Lernkicker:DataPath", _dataPath);
        builder.UseSetting("Admin:Email", AdminEmail);
        builder.UseSetting("Admin:Password", AdminPassword);
        builder.UseSetting("Admin:Name", "Robin");
        builder.UseSetting("Admin:Family", "Family Admin");
        builder.UseSetting("Lernkicker:Limits:LoginsPerMinute", LimitPerMinute.ToString());
        builder.UseSetting("Lernkicker:Limits:ChildLoginsPerMinute", LimitPerMinute.ToString());
        builder.UseSetting("Lernkicker:Limits:CalendarRequestsPerMinute", LimitPerMinute.ToString());
        builder.UseSetting("Lernkicker:Calendar:SyncHours", "0");
        if (PublicUrl is not null)
        {
            builder.UseSetting("Lernkicker:PublicUrl", PublicUrl);
        }
        builder.ConfigureTestServices(services => services.AddSingleton<ICalendarSource>(Calendars));
    }

    /// <summary>A browser-like client with its own cookies.</summary>
    public HttpClient Client() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });

    public async Task<HttpClient> AdminAsync()
    {
        var client = Client();
        var response = await client.PostAsJsonAsync("/api/account/login", new Login(AdminEmail, AdminPassword));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return client;
    }

    /// <summary>Creates a new family through an invitation and returns its logged-in parent.</summary>
    public async Task<HttpClient> NewFamilyAsync(string email)
    {
        var admin = await AdminAsync();
        var invitation = await Json<InvitationCreatedResponse>(await admin.PostAsJsonAsync("/api/admin/invitations", new NewInvitation(null, $"Family {email}")));

        var client = Client();
        var response = await client.PostAsJsonAsync("/api/account/invitation", new RedeemInvitationRequest(invitation.Code, email, "parent-password-123", email, true, "en"));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            Directory.Delete(_dataPath, recursive: true);
        }
        catch (IOException)
        {
            // SQLite may still hold the file briefly; the temp folder is cleaned up eventually
        }
    }

    public static async Task<T> Json<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(ApiJson.Options))!;
    }

    public static async Task<ApiError> Error(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiError>(ApiJson.Options))!;
    }
}

/// <summary>Answers calendar fetches from a table instead of the internet; unknown URLs are unreachable.</summary>
public sealed class FakeCalendarSource : ICalendarSource
{
    private readonly ConcurrentDictionary<string, Result<IReadOnlyList<ImportedEvent>>> _answers = new();

    public void Set(string url, params ImportedEvent[] events) => _answers[url] = Result<IReadOnlyList<ImportedEvent>>.Ok(events);

    public void Fail(string url, string errorCode) => _answers[url] = AppError.Invalid(errorCode);

    public Task<Result<IReadOnlyList<ImportedEvent>>> FetchAsync(Uri url, DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
        Task.FromResult(_answers.TryGetValue(url.ToString(), out var answer) ? answer : AppError.Invalid(ErrorCodes.CalendarUnreachable));
}

public static class TestData
{
    public static string HsuJson() => Lernkicker.Tests.Examples.HsuJson();

    public static HttpContent AsJson(string json) => new StringContent(json, System.Text.Encoding.UTF8, "application/json");

    public static async Task<QuizOverview> UploadHsuAsync(HttpClient parent) =>
        await LernkickerFactory.Json<QuizOverview>(await parent.PostAsync("/api/quizzes", AsJson(HsuJson())));

    public static async Task<ChildOverview> CreateChildAsync(HttpClient parent, string name = "Calvin", string pin = "1234") =>
        await LernkickerFactory.Json<ChildOverview>(await parent.PostAsJsonAsync("/api/children", new NewChild(name, 4, pin)));

    public static async Task<AssignmentOverview> AssignAsync(HttpClient parent, Guid childId, string quizId, DateOnly? dueOn = null) =>
        await LernkickerFactory.Json<AssignmentOverview>(await parent.PostAsJsonAsync($"/api/children/{childId}/assignments", new NewAssignment(quizId, dueOn)));

    public static async Task<LessonOverview> UploadPoemAsync(HttpClient parent) =>
        await LernkickerFactory.Json<LessonOverview>(await parent.PostAsync("/api/lessons", AsJson(Lernkicker.Tests.Examples.PoemJson())));

    public static async Task<LessonAssignmentOverview> AssignLessonAsync(HttpClient parent, Guid childId, string lessonId, DateOnly? dueOn = null) =>
        await LernkickerFactory.Json<LessonAssignmentOverview>(await parent.PostAsJsonAsync($"/api/children/{childId}/lessons", new NewLessonAssignment(lessonId, dueOn)));

    /// <summary>Pairs a new device for the child and logs in with the PIN.</summary>
    public static async Task<HttpClient> ChildPhoneAsync(LernkickerFactory factory, HttpClient parent, Guid childId, string pin = "1234")
    {
        var pairing = await LernkickerFactory.Json<PairingCodeResponse>(await parent.PostAsync($"/api/children/{childId}/pairing", null));
        var phone = factory.Client();
        await LernkickerFactory.Json<DeviceStatusResponse>(await phone.PostAsJsonAsync("/api/device/pair", new PairDevice(pairing.Code, "Test phone")));
        var login = await phone.PostAsJsonAsync("/api/device/login", new PinLogin(pin));
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        return phone;
    }

    public static Dictionary<string, AnswerDto> AllCorrect(Quiz quiz) =>
        Lernkicker.Tests.Examples.AllCorrect(quiz).ToDictionary(a => a.Key, a => new AnswerDto
        {
            Texts = a.Value.Texts,
            Truths = a.Value.Truths,
            Order = a.Value.Order,
            Choices = a.Value.Choices,
        });
}
