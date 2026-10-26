using System.Net;
using System.Net.Http.Json;
using Lernkicker.Contracts;

namespace Lernkicker.Client.Services;

/// <summary>A failed API call. <c>Error</c> holds the server's code; <see cref="Display.Errors"/> translates it.</summary>
public sealed class ApiException(HttpStatusCode status, ApiError? error) : Exception(error?.Code ?? status.ToString())
{
    public HttpStatusCode Status { get; } = status;
    public ApiError? Error { get; } = error;
}

/// <summary>Typed calls to the Lernkicker server. Cookies are sent automatically (same origin).</summary>
public sealed class Api(HttpClient http)
{
    // First-run setup
    public async Task<bool> SetupNeeded() => (await Get<SetupStatus>("api/setup")).Needed;
    public Task CompleteSetup(SetupRequest request) => Send(HttpMethod.Post, "api/setup", request);

    public async Task RestoreBackup(Stream zip)
    {
        using var content = new StreamContent(zip);
        content.Headers.ContentType = new("application/zip");
        await Check(await http.PostAsync("api/setup/restore", content));
    }

    /// <summary>True when the server answers, e.g. again after a restart.</summary>
    public async Task<bool> IsUp()
    {
        try
        {
            return (await http.GetAsync("api/status")).IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    // Account
    public Task Login(string email, string password) => Send(HttpMethod.Post, "api/account/login", new Login(email, password));
    public Task Logout() => Send(HttpMethod.Post, "api/account/logout");
    public Task RedeemInvitation(RedeemInvitationRequest request) => Send(HttpMethod.Post, "api/account/invitation", request);
    public Task SetLanguage(string language) => Send(HttpMethod.Put, "api/account/language", new SetLanguage(language));

    // Telegram for the logged-in parent
    public Task<TelegramStatusDto> TelegramStatus() => Get<TelegramStatusDto>("api/account/telegram");
    public Task<TelegramLinkDto> CreateTelegramLink() => Send<TelegramLinkDto>(HttpMethod.Post, "api/account/telegram/link");
    public Task UnlinkTelegram() => Send(HttpMethod.Delete, "api/account/telegram");

    /// <summary>The logged-in parent, or null.</summary>
    public async Task<Me?> Me()
    {
        var response = await http.GetAsync("api/account/me");
        return response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ? null : await Read<Me>(response);
    }

    // Admin
    public Task<InvitationCreatedResponse> CreateInvitation(NewInvitation request) => Send<InvitationCreatedResponse>(HttpMethod.Post, "api/admin/invitations", request);
    public Task<List<FamilyOverview>> Families() => Get<List<FamilyOverview>>("api/admin/families");
    public Task DeleteFamily(Guid id) => Send(HttpMethod.Delete, $"api/admin/families/{id}");
    public Task<BuildInfoDto> ServerVersion() => Get<BuildInfoDto>("api/admin/version");
    public Task<TelegramBotDto> TelegramBot() => Get<TelegramBotDto>("api/admin/telegram");
    public Task SetBotToken(string token) => Send(HttpMethod.Put, "api/admin/telegram", new SetBotToken(token));
    public Task RemoveBotToken() => Send(HttpMethod.Delete, "api/admin/telegram");

    // Quizzes
    public Task<List<QuizOverview>> Quizzes() => Get<List<QuizOverview>>("api/quizzes");

    public async Task<QuizOverview> UploadQuiz(string json)
    {
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        return await Read<QuizOverview>(await http.PostAsync("api/quizzes", content));
    }

    /// <summary>A quiz with its pictures, or a zip (see <see cref="UploadFiles"/>).</summary>
    public async Task<QuizOverview> UploadQuiz(HttpContent files) => await Read<QuizOverview>(await http.PostAsync("api/quizzes", files));

    public Task<QuizOverview> ShareQuiz(string quizId, bool shared) => Send<QuizOverview>(HttpMethod.Put, $"api/quizzes/{Uri.EscapeDataString(quizId)}/shared", new ShareRequest(shared));
    public Task<SheetDto> QuizPreview(string quizId) => Get<SheetDto>($"api/quizzes/{Uri.EscapeDataString(quizId)}/preview");

    public Task<QuizDeletedResponse> DeleteQuiz(string quizId) => Send<QuizDeletedResponse>(HttpMethod.Delete, $"api/quizzes/{Uri.EscapeDataString(quizId)}");

    // Lessons
    public Task<List<LessonOverview>> Lessons() => Get<List<LessonOverview>>("api/lessons");

    public async Task<LessonOverview> UploadLesson(string json)
    {
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        return await Read<LessonOverview>(await http.PostAsync("api/lessons", content));
    }

    /// <summary>A lesson with its pictures, or a zip (see <see cref="UploadFiles"/>).</summary>
    public async Task<LessonOverview> UploadLesson(HttpContent files) => await Read<LessonOverview>(await http.PostAsync("api/lessons", files));

    public Task<LessonOverview> ShareLesson(string lessonId, bool shared) => Send<LessonOverview>(HttpMethod.Put, $"api/lessons/{Uri.EscapeDataString(lessonId)}/shared", new ShareRequest(shared));
    public Task<LessonDto> LessonPreview(string lessonId) => Get<LessonDto>($"api/lessons/{Uri.EscapeDataString(lessonId)}/preview");

    // Shared library (what other families shared)
    public Task<List<SharedOverview>> Library() => Get<List<SharedOverview>>("api/library");
    public Task<SheetDto> SharedQuiz(Guid id) => Get<SheetDto>($"api/library/quizzes/{id}");
    public Task<LessonDto> SharedLesson(Guid id) => Get<LessonDto>($"api/library/lessons/{id}");
    public Task<QuizOverview> CopyQuiz(Guid id) => Send<QuizOverview>(HttpMethod.Post, $"api/library/quizzes/{id}/copy");
    public Task<LessonOverview> CopyLesson(Guid id) => Send<LessonOverview>(HttpMethod.Post, $"api/library/lessons/{id}/copy");

    public Task DeleteLesson(string lessonId) => Send(HttpMethod.Delete, $"api/lessons/{Uri.EscapeDataString(lessonId)}");

    // Children (for parents)
    public Task<List<ChildOverview>> Children() => Get<List<ChildOverview>>("api/children");
    public Task<ChildOverview> CreateChild(NewChild request) => Send<ChildOverview>(HttpMethod.Post, "api/children", request);
    public Task UpdateChild(Guid id, ChildDetails request) => Send(HttpMethod.Put, $"api/children/{id}", request);
    public Task ChangePin(Guid id, string pin) => Send(HttpMethod.Put, $"api/children/{id}/pin", new ChangePin(pin));
    public Task DeleteChild(Guid id) => Send(HttpMethod.Delete, $"api/children/{id}");
    public Task LearnMyself() => Send(HttpMethod.Post, "api/children/learn");
    public Task LearnAs(Guid id) => Send(HttpMethod.Post, $"api/children/{id}/learn");
    public Task<PairingCodeResponse> CreatePairingCode(Guid id) => Send<PairingCodeResponse>(HttpMethod.Post, $"api/children/{id}/pairing");
    public Task<List<DeviceOverview>> Devices(Guid id) => Get<List<DeviceOverview>>($"api/children/{id}/devices");
    public Task RemoveDevice(Guid id, Guid deviceId) => Send(HttpMethod.Delete, $"api/children/{id}/devices/{deviceId}");
    public Task UnlockDevice(Guid id, Guid deviceId) => Send(HttpMethod.Post, $"api/children/{id}/devices/{deviceId}/unlock");
    public Task<List<AssignmentOverview>> Assignments(Guid id) => Get<List<AssignmentOverview>>($"api/children/{id}/assignments");
    public Task<AssignmentOverview> Assign(Guid id, NewAssignment request) => Send<AssignmentOverview>(HttpMethod.Post, $"api/children/{id}/assignments", request);
    public Task Unassign(Guid id, Guid assignmentId) => Send(HttpMethod.Delete, $"api/children/{id}/assignments/{assignmentId}");
    public Task<List<LessonAssignmentOverview>> LessonAssignments(Guid id) => Get<List<LessonAssignmentOverview>>($"api/children/{id}/lessons");
    public Task<LessonAssignmentOverview> AssignLesson(Guid id, NewLessonAssignment request) => Send<LessonAssignmentOverview>(HttpMethod.Post, $"api/children/{id}/lessons", request);
    public Task UnassignLesson(Guid id, Guid assignmentId) => Send(HttpMethod.Delete, $"api/children/{id}/lessons/{assignmentId}");
    public Task<List<AttemptOverview>> Attempts(Guid id) => Get<List<AttemptOverview>>($"api/children/{id}/attempts");
    public Task<QuizResultDto> Attempt(Guid id, Guid attemptId) => Get<QuizResultDto>($"api/children/{id}/attempts/{attemptId}");

    // Playing with others (for parents)
    public Task<PlaySettingsDto> PlaySettings(Guid id) => Get<PlaySettingsDto>($"api/children/{id}/play");
    public Task<PlaySettingsDto> SavePlaySettings(Guid id, PlaySettingsDto settings) => Send<PlaySettingsDto>(HttpMethod.Put, $"api/children/{id}/play", settings);
    public Task<PlayPartnersDto> PlayPartners(Guid id) => Get<PlayPartnersDto>($"api/children/{id}/play/partners");

    // Calendar (for parents)
    public Task<List<CalendarEntryDto>> Calendar(Guid id) => Get<List<CalendarEntryDto>>($"api/children/{id}/calendar");
    public Task<List<CalendarEntryDto>> Calendar(Guid id, DateOnly from, DateOnly to) => Get<List<CalendarEntryDto>>($"api/children/{id}/calendar?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
    public Task<CalendarEntryDto> AddEvent(Guid id, EventRequest request) => Send<CalendarEntryDto>(HttpMethod.Post, $"api/children/{id}/calendar", request);
    public Task<CalendarEntryDto> UpdateEvent(Guid id, Guid eventId, EventRequest request) => Send<CalendarEntryDto>(HttpMethod.Put, $"api/children/{id}/calendar/{eventId}", request);
    public Task DeleteEvent(Guid id, Guid eventId) => Send(HttpMethod.Delete, $"api/children/{id}/calendar/{eventId}");
    public Task<List<SubscriptionOverview>> Subscriptions(Guid id) => Get<List<SubscriptionOverview>>($"api/children/{id}/subscriptions");
    public Task<SubscriptionOverview> Subscribe(Guid id, NewSubscription request) => Send<SubscriptionOverview>(HttpMethod.Post, $"api/children/{id}/subscriptions", request);
    public Task<SubscriptionOverview> SyncSubscription(Guid id, Guid subscriptionId) => Send<SubscriptionOverview>(HttpMethod.Post, $"api/children/{id}/subscriptions/{subscriptionId}/sync");
    public Task Unsubscribe(Guid id, Guid subscriptionId) => Send(HttpMethod.Delete, $"api/children/{id}/subscriptions/{subscriptionId}");
    public Task<FeedResponse> CreateFeed(Guid id) => Send<FeedResponse>(HttpMethod.Post, $"api/children/{id}/feed");
    public Task RemoveFeed(Guid id) => Send(HttpMethod.Delete, $"api/children/{id}/feed");

    // Child's device
    public Task<DeviceStatusResponse> DeviceStatus() => Get<DeviceStatusResponse>("api/device/status");
    public Task<DeviceStatusResponse> Pair(string code, string? label) => Send<DeviceStatusResponse>(HttpMethod.Post, "api/device/pair", new PairDevice(code, label));
    public Task ChildLogin(string pin) => Send(HttpMethod.Post, "api/device/login", new PinLogin(pin));
    public Task ChildLogout() => Send(HttpMethod.Post, "api/device/logout");

    /// <summary>The child's assignments, or null when the child is not logged in.</summary>
    public async Task<List<MyAssignment>?> MyAssignments()
    {
        var response = await http.GetAsync("api/device/assignments");
        return response.StatusCode == HttpStatusCode.Unauthorized ? null : await Read<List<MyAssignment>>(response);
    }

    /// <summary>The child's next dates, or null when the child is not logged in.</summary>
    public async Task<List<CalendarEntryDto>?> MyCalendar()
    {
        var response = await http.GetAsync("api/device/calendar");
        return response.StatusCode == HttpStatusCode.Unauthorized ? null : await Read<List<CalendarEntryDto>>(response);
    }

    /// <summary>The child's lessons, or null when the child is not logged in.</summary>
    public async Task<List<MyLesson>?> MyLessons()
    {
        var response = await http.GetAsync("api/device/lessons");
        return response.StatusCode == HttpStatusCode.Unauthorized ? null : await Read<List<MyLesson>>(response);
    }

    public Task<LessonDto> Lesson(Guid assignmentId) => Get<LessonDto>($"api/device/lessons/{assignmentId}");

    public Task<LessonGameDto> LessonGame(Guid assignmentId, string kind) => Get<LessonGameDto>($"api/device/lessons/{assignmentId}/games/{Uri.EscapeDataString(kind)}");

    public Task<LessonDto> Practised(Guid assignmentId, IReadOnlyList<string> knownBlocks) =>
        Send<LessonDto>(HttpMethod.Post, $"api/device/lessons/{assignmentId}/practice", new LessonPractice(knownBlocks));

    public Task<SheetDto> Sheet(Guid assignmentId) => Get<SheetDto>($"api/device/assignments/{assignmentId}");

    public Task<QuizResultDto> Submit(Guid assignmentId, Dictionary<string, AnswerDto> answers) =>
        Send<QuizResultDto>(HttpMethod.Post, $"api/device/assignments/{assignmentId}/submit", new Submission(answers));

    // Playing with others (child's device)
    /// <summary>The lobby, or null when the child is not logged in.</summary>
    public async Task<LobbyDto?> Lobby()
    {
        var response = await http.GetAsync("api/play/lobby");
        return response.StatusCode == HttpStatusCode.Unauthorized ? null : await Read<LobbyDto>(response);
    }

    public Task<MatchDto> OpenTable(string game) => Send<MatchDto>(HttpMethod.Post, "api/play/tables", new NewTable(game));
    public Task<MatchDto> JoinTable(Guid id) => Send<MatchDto>(HttpMethod.Post, $"api/play/tables/{id}/join");
    public Task LeaveTable(Guid id) => Send(HttpMethod.Delete, $"api/play/tables/{id}");
    public Task<MatchDto> Match(Guid id) => Get<MatchDto>($"api/play/matches/{id}");
    public Task<MatchDto> Move(Guid id, MoveDto move) => Send<MatchDto>(HttpMethod.Post, $"api/play/matches/{id}/moves", move, typeof(MoveDto));
    public Task<MatchDto> Resign(Guid id) => Send<MatchDto>(HttpMethod.Post, $"api/play/matches/{id}/resign");
    public Task<List<ShipDto>> RandomFleet() => Get<List<ShipDto>>("api/play/battleship/random-fleet");

    private async Task<T> Get<T>(string url) => await Read<T>(await http.GetAsync(url));

    /// <summary><paramref name="bodyType"/>: the declared type of a polymorphic body, so its "type" is sent.</summary>
    private async Task<T> Send<T>(HttpMethod method, string url, object? body = null, Type? bodyType = null) => await Read<T>(await Exchange(method, url, body, bodyType));

    private async Task Send(HttpMethod method, string url, object? body = null) => await Check(await Exchange(method, url, body));

    private Task<HttpResponseMessage> Exchange(HttpMethod method, string url, object? body, Type? bodyType = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, bodyType ?? body.GetType(), options: ApiJson.Options);
        }

        return http.SendAsync(request);
    }

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        await Check(response);
        return (await response.Content.ReadFromJsonAsync<T>(ApiJson.Options))!;
    }

    private static async Task Check(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        ApiError? error = null;
        try
        {
            error = await response.Content.ReadFromJsonAsync<ApiError>(ApiJson.Options);
        }
        catch (Exception e) when (e is System.Text.Json.JsonException or NotSupportedException)
        {
            // Not our error format (e.g. a proxy page or the rate limiter); the status decides the message
        }

        throw new ApiException(response.StatusCode, error);
    }
}
