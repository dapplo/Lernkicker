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
    // Account
    public Task Login(string email, string password) => Send(HttpMethod.Post, "api/account/login", new Login(email, password));
    public Task Logout() => Send(HttpMethod.Post, "api/account/logout");
    public Task RedeemInvitation(RedeemInvitationRequest request) => Send(HttpMethod.Post, "api/account/invitation", request);
    public Task SetLanguage(string language) => Send(HttpMethod.Put, "api/account/language", new SetLanguage(language));

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

    // Quizzes
    public Task<List<QuizOverview>> Quizzes() => Get<List<QuizOverview>>("api/quizzes");

    public async Task<QuizOverview> UploadQuiz(string json)
    {
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        return await Read<QuizOverview>(await http.PostAsync("api/quizzes", content));
    }

    public Task<QuizDeletedResponse> DeleteQuiz(string quizId) => Send<QuizDeletedResponse>(HttpMethod.Delete, $"api/quizzes/{Uri.EscapeDataString(quizId)}");

    // Children (for parents)
    public Task<List<ChildOverview>> Children() => Get<List<ChildOverview>>("api/children");
    public Task<ChildOverview> CreateChild(NewChild request) => Send<ChildOverview>(HttpMethod.Post, "api/children", request);
    public Task UpdateChild(Guid id, ChildDetails request) => Send(HttpMethod.Put, $"api/children/{id}", request);
    public Task ChangePin(Guid id, string pin) => Send(HttpMethod.Put, $"api/children/{id}/pin", new ChangePin(pin));
    public Task DeleteChild(Guid id) => Send(HttpMethod.Delete, $"api/children/{id}");
    public Task<PairingCodeResponse> CreatePairingCode(Guid id) => Send<PairingCodeResponse>(HttpMethod.Post, $"api/children/{id}/pairing");
    public Task<List<DeviceOverview>> Devices(Guid id) => Get<List<DeviceOverview>>($"api/children/{id}/devices");
    public Task RemoveDevice(Guid id, Guid deviceId) => Send(HttpMethod.Delete, $"api/children/{id}/devices/{deviceId}");
    public Task UnlockDevice(Guid id, Guid deviceId) => Send(HttpMethod.Post, $"api/children/{id}/devices/{deviceId}/unlock");
    public Task<List<AssignmentOverview>> Assignments(Guid id) => Get<List<AssignmentOverview>>($"api/children/{id}/assignments");
    public Task<AssignmentOverview> Assign(Guid id, NewAssignment request) => Send<AssignmentOverview>(HttpMethod.Post, $"api/children/{id}/assignments", request);
    public Task Unassign(Guid id, Guid assignmentId) => Send(HttpMethod.Delete, $"api/children/{id}/assignments/{assignmentId}");
    public Task<List<AttemptOverview>> Attempts(Guid id) => Get<List<AttemptOverview>>($"api/children/{id}/attempts");
    public Task<QuizResultDto> Attempt(Guid id, Guid attemptId) => Get<QuizResultDto>($"api/children/{id}/attempts/{attemptId}");

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

    public Task<SheetDto> Sheet(Guid assignmentId) => Get<SheetDto>($"api/device/assignments/{assignmentId}");

    public Task<QuizResultDto> Submit(Guid assignmentId, Dictionary<string, AnswerDto> answers) =>
        Send<QuizResultDto>(HttpMethod.Post, $"api/device/assignments/{assignmentId}/submit", new Submission(answers));

    private async Task<T> Get<T>(string url) => await Read<T>(await http.GetAsync(url));

    private async Task<T> Send<T>(HttpMethod method, string url, object? body = null) => await Read<T>(await Exchange(method, url, body));

    private async Task Send(HttpMethod method, string url, object? body = null) => await Check(await Exchange(method, url, body));

    private Task<HttpResponseMessage> Exchange(HttpMethod method, string url, object? body)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: ApiJson.Options);
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
