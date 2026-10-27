using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lernkicker.Contracts;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Lernkicker.Web.Tests;

/// <summary>AI assistants over MCP with a family's API key (docs/mcp.md), and the keys themselves.</summary>
public sealed class McpTests(LernkickerFactory factory) : IClassFixture<LernkickerFactory>
{
    private static readonly string[] WriteTools =
    [
        "upload_quiz", "delete_quiz", "upload_lesson", "delete_lesson", "assign_quiz", "unassign_quiz", "assign_lesson", "unassign_lesson",
        "add_calendar_event", "update_calendar_event", "delete_calendar_event",
        "create_child", "update_child", "delete_child", "set_child_pin", "create_pairing_link", "unlock_device", "remove_device",
    ];

    [Fact]
    public async Task Parent_creates_lists_and_revokes_keys_and_the_key_is_shown_once()
    {
        var parent = await factory.NewFamilyAsync("keys@example.org");
        var created = await CreateKeyAsync(parent, "Claude");

        Assert.StartsWith("lk_", created.Token);
        Assert.Equal("https://localhost/mcp", created.McpUrl);
        Assert.Equal(created.Token[^4..], created.Key.Hint);
        Assert.False(created.Key.ReadOnly);

        var listed = Assert.Single(await LernkickerFactory.Json<List<ApiKeyOverview>>(await parent.GetAsync("/api/api-keys")));
        Assert.Equal("Claude", listed.Name);
        Assert.DoesNotContain(created.Token, await (await parent.GetAsync("/api/api-keys")).Content.ReadAsStringAsync());

        await using (var mcp = await ConnectAsync(created.Token))
        {
            Assert.Equal("lernkicker", mcp.ServerInfo?.Name);
        }

        Assert.NotNull(Assert.Single(await LernkickerFactory.Json<List<ApiKeyOverview>>(await parent.GetAsync("/api/api-keys"))).LastUsedAt);

        Assert.Equal(HttpStatusCode.NoContent, (await parent.DeleteAsync($"/api/api-keys/{created.Key.Id}")).StatusCode);
        Assert.Empty(await LernkickerFactory.Json<List<ApiKeyOverview>>(await parent.GetAsync("/api/api-keys")));
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostInitializeAsync(created.Token)).StatusCode);
    }

    [Fact]
    public async Task Invalid_names_and_too_many_keys_are_refused()
    {
        var parent = await factory.NewFamilyAsync("many@example.org");
        Assert.Equal("apiKey.nameInvalid", (await LernkickerFactory.Error(await parent.PostAsJsonAsync("/api/api-keys", new NewApiKey(" ", false)), HttpStatusCode.BadRequest)).Code);

        for (var i = 0; i < 10; i++)
        {
            await CreateKeyAsync(parent, $"Key {i}");
        }

        Assert.Equal("apiKey.tooMany", (await LernkickerFactory.Error(await parent.PostAsJsonAsync("/api/api-keys", new NewApiKey("One more", false)), HttpStatusCode.BadRequest)).Code);
    }

    [Fact]
    public async Task Mcp_needs_a_valid_key_and_ignores_the_parent_login()
    {
        var parent = await factory.NewFamilyAsync("nokey@example.org");

        Assert.Equal(HttpStatusCode.Unauthorized, (await PostInitializeAsync(null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostInitializeAsync("lk_not-a-real-key")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await parent.PostAsync("/mcp", InitializeBody())).StatusCode);

        // The key opens nothing but the MCP endpoint
        var key = await CreateKeyAsync(parent);
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new("Bearer", key.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/api/quizzes")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/api/api-keys")).StatusCode);
    }

    [Fact]
    public async Task Assistant_uploads_quiz_and_lesson_and_puts_them_on_the_childs_list()
    {
        var parent = await factory.NewFamilyAsync("assistant@example.org");
        var child = await TestData.CreateChildAsync(parent);
        var key = await CreateKeyAsync(parent);
        await using var mcp = await ConnectAsync(key.Token);

        var tools = (await mcp.ListToolsAsync()).Select(t => t.Name).ToList();
        Assert.Contains("get_authoring_guide", tools);
        Assert.All(WriteTools, t => Assert.Contains(t, tools));
        Assert.Contains("quiz-v1.schema.json", Text(await mcp.CallToolAsync("get_authoring_guide", new Dictionary<string, object?> { ["language"] = "de" })));

        var children = await CallAsync<List<JsonElement>>(mcp, "list_children");
        Assert.Equal(child.Id, Assert.Single(children).GetProperty("id").GetGuid());

        Assert.True((await CallAsync<JsonElement>(mcp, "validate_quiz", new() { ["json"] = TestData.HsuJson() })).GetProperty("valid").GetBoolean());
        Assert.Empty(await LernkickerFactory.Json<List<QuizOverview>>(await parent.GetAsync("/api/quizzes")));

        var quiz = await CallAsync<QuizOverview>(mcp, "upload_quiz", new() { ["json"] = TestData.HsuJson() });
        Assert.Equal(quiz.QuizId, Assert.Single(await LernkickerFactory.Json<List<QuizOverview>>(await parent.GetAsync("/api/quizzes"))).QuizId);
        Assert.Equal(TestData.HsuJson(), (await CallAsync<JsonElement>(mcp, "get_quiz", new() { ["quizId"] = quiz.QuizId })).GetProperty("json").GetString());

        var due = new DateOnly(2026, 11, 20);
        var assignment = await CallAsync<AssignmentOverview>(mcp, "assign_quiz", new() { ["childId"] = child.Id, ["quizId"] = quiz.QuizId, ["dueOn"] = "2026-11-20" });
        Assert.Equal(due, assignment.DueOn);
        Assert.Equal(due, Assert.Single(await LernkickerFactory.Json<List<AssignmentOverview>>(await parent.GetAsync($"/api/children/{child.Id}/assignments"))).DueOn);

        var lesson = await CallAsync<LessonOverview>(mcp, "upload_lesson", new() { ["json"] = Lernkicker.Tests.Examples.PoemJson() });
        await CallAsync<LessonAssignmentOverview>(mcp, "assign_lesson", new() { ["childId"] = child.Id, ["lessonId"] = lesson.LessonId, ["dueOn"] = "2026-11-18" });

        var todo = await CallAsync<JsonElement>(mcp, "list_assignments", new() { ["childId"] = child.Id });
        Assert.Equal(1, todo.GetProperty("quizzes").GetArrayLength());
        Assert.Equal(1, todo.GetProperty("lessons").GetArrayLength());

        var calendarEvent = await CallAsync<CalendarEntryDto>(mcp, "add_calendar_event",
            new() { ["childId"] = child.Id, ["kind"] = "test", ["title"] = "HSU-Probe", ["date"] = "2026-11-20", ["startTime"] = "08:00" });
        var calendar = await CallAsync<List<CalendarEntryDto>>(mcp, "get_calendar", new() { ["childId"] = child.Id, ["from"] = "2026-11-01", ["to"] = "2026-11-30" });
        Assert.Contains(calendar, e => e.Source == "assignment" && e.Date == due);
        Assert.Contains(calendar, e => e.Source == "lesson" && e.Date == new DateOnly(2026, 11, 18));
        Assert.Contains(calendar, e => e.EventId == calendarEvent.EventId && e.StartTime == new TimeOnly(8, 0));

        await CallAsync<string>(mcp, "unassign_quiz", new() { ["childId"] = child.Id, ["assignmentId"] = assignment.Id });
        Assert.Empty(await LernkickerFactory.Json<List<AssignmentOverview>>(await parent.GetAsync($"/api/children/{child.Id}/assignments")));
        Assert.Empty(await CallAsync<List<AttemptOverview>>(mcp, "list_results", new() { ["childId"] = child.Id }));
    }

    [Fact]
    public async Task Assistant_manages_the_familys_learner_profiles()
    {
        var parent = await factory.NewFamilyAsync("profiles@example.org");
        var key = await CreateKeyAsync(parent);
        await using var mcp = await ConnectAsync(key.Token);

        // An adult who learns with their own parent login: no PIN, no phone
        var adult = await CallAsync<JsonElement>(mcp, "create_child", new() { ["name"] = "Melanie", ["isAdult"] = true });
        Assert.False(adult.GetProperty("hasPin").GetBoolean());
        var adultId = adult.GetProperty("id").GetGuid();
        var overview = Assert.Single(await LernkickerFactory.Json<List<ChildOverview>>(await parent.GetAsync("/api/children")));
        Assert.True(overview.IsAdult);
        Assert.Equal(HttpStatusCode.NoContent, (await parent.PostAsync($"/api/children/{adultId}/learn", null)).StatusCode);
        Assert.True((await CallAsync<List<JsonElement>>(mcp, "list_children")).Single().GetProperty("parentLogin").GetBoolean());

        var child = await CallAsync<JsonElement>(mcp, "create_child", new() { ["name"] = "Lena", ["gradeLevel"] = 2, ["pin"] = "1234", ["theme"] = "space" });
        var childId = child.GetProperty("id").GetGuid();
        Assert.True(child.GetProperty("hasPin").GetBoolean());

        var updated = await CallAsync<JsonElement>(mcp, "update_child", new() { ["childId"] = childId, ["gradeLevel"] = 3 });
        Assert.Equal("Lena", updated.GetProperty("name").GetString());
        Assert.Equal(3, updated.GetProperty("gradeLevel").GetInt32());
        Assert.Equal("space", updated.GetProperty("theme").GetString());

        Assert.Contains("child.gradeLevelInvalid", Text(await mcp.CallToolAsync("update_child", new Dictionary<string, object?> { ["childId"] = childId, ["gradeLevel"] = 14 })));
        Assert.Contains("pin.invalid", Text(await mcp.CallToolAsync("set_child_pin", new Dictionary<string, object?> { ["childId"] = childId, ["pin"] = "12" })));
        await CallAsync<string>(mcp, "set_child_pin", new() { ["childId"] = childId, ["pin"] = "4321" });

        var pairing = await CallAsync<JsonElement>(mcp, "create_pairing_link", new() { ["childId"] = childId });
        Assert.EndsWith($"/pair#{pairing.GetProperty("code").GetString()}", pairing.GetProperty("link").GetString());
        Assert.Empty(await CallAsync<List<DeviceOverview>>(mcp, "list_devices", new() { ["childId"] = childId }));

        await CallAsync<string>(mcp, "delete_child", new() { ["childId"] = childId });
        Assert.Equal(adultId, Assert.Single(await LernkickerFactory.Json<List<ChildOverview>>(await parent.GetAsync("/api/children"))).Id);
    }

    [Fact]
    public async Task Invalid_files_come_back_with_codes_and_issues()
    {
        var parent = await factory.NewFamilyAsync("invalid@example.org");
        var key = await CreateKeyAsync(parent);
        await using var mcp = await ConnectAsync(key.Token);

        var broken = await mcp.CallToolAsync("upload_quiz", new Dictionary<string, object?> { ["json"] = """{ "format": "quiz/v1" }""" });
        Assert.True(broken.IsError);
        var error = JsonSerializer.Deserialize<ApiError>(Text(broken)[Text(broken).IndexOf('{')..], ApiJson.Options)!;
        Assert.Equal("quiz.invalid", error.Code);
        Assert.NotEmpty(error.Issues!);

        var picture = await mcp.CallToolAsync("validate_lesson", new Dictionary<string, object?>
        {
            ["json"] = Lernkicker.Tests.Examples.PoemJson(),
            ["pictures"] = new[] { new { name = "x.png", base64 = "not base64!" } },
        });
        Assert.True(picture.IsError);
        Assert.Contains("upload.invalidPicture", Text(picture));

        var missing = await mcp.CallToolAsync("get_quiz", new Dictionary<string, object?> { ["quizId"] = "nope" });
        Assert.True(missing.IsError);
        Assert.Contains("quiz.notFound", Text(missing));
    }

    [Fact]
    public async Task Read_only_key_sees_everything_but_changes_nothing()
    {
        var parent = await factory.NewFamilyAsync("readonly@example.org");
        var child = await TestData.CreateChildAsync(parent);
        var quiz = await TestData.UploadHsuAsync(parent);
        var key = await CreateKeyAsync(parent, "Reader", readOnly: true);
        Assert.True(key.Key.ReadOnly);
        await using var mcp = await ConnectAsync(key.Token);

        var tools = (await mcp.ListToolsAsync()).Select(t => t.Name).ToList();
        Assert.Contains("list_quizzes", tools);
        Assert.Contains("validate_quiz", tools);
        Assert.All(WriteTools, t => Assert.DoesNotContain(t, tools));

        Assert.Single(await CallAsync<List<QuizOverview>>(mcp, "list_quizzes"));
        await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await mcp.CallToolAsync("assign_quiz", new Dictionary<string, object?> { ["childId"] = child.Id, ["quizId"] = quiz.QuizId }));
        Assert.Empty(await LernkickerFactory.Json<List<AssignmentOverview>>(await parent.GetAsync($"/api/children/{child.Id}/assignments")));
    }

    [Fact]
    public async Task Key_of_another_family_sees_and_changes_nothing()
    {
        var a = await factory.NewFamilyAsync("mcp-a@example.org");
        var b = await factory.NewFamilyAsync("mcp-b@example.org");
        var child = await TestData.CreateChildAsync(a);
        var quiz = await TestData.UploadHsuAsync(a);
        var keyA = await CreateKeyAsync(a);
        var keyB = await CreateKeyAsync(b);
        await using var mcp = await ConnectAsync(keyB.Token);

        Assert.Empty(await CallAsync<List<JsonElement>>(mcp, "list_children"));
        Assert.Empty(await CallAsync<List<QuizOverview>>(mcp, "list_quizzes"));
        Assert.True((await mcp.CallToolAsync("get_quiz", new Dictionary<string, object?> { ["quizId"] = quiz.QuizId })).IsError);
        Assert.True((await mcp.CallToolAsync("get_calendar", new Dictionary<string, object?> { ["childId"] = child.Id })).IsError);
        Assert.True((await mcp.CallToolAsync("list_assignments", new Dictionary<string, object?> { ["childId"] = child.Id })).IsError);
        Assert.Empty(await CallAsync<List<AttemptOverview>>(mcp, "list_results", new() { ["childId"] = child.Id }));

        // B's own copy of the quiz can't be given to A's child, nor can B add dates to A's child's calendar
        await CallAsync<QuizOverview>(mcp, "upload_quiz", new() { ["json"] = TestData.HsuJson() });
        Assert.True((await mcp.CallToolAsync("assign_quiz", new Dictionary<string, object?> { ["childId"] = child.Id, ["quizId"] = quiz.QuizId })).IsError);
        Assert.True((await mcp.CallToolAsync("add_calendar_event", new Dictionary<string, object?>
            { ["childId"] = child.Id, ["kind"] = "test", ["title"] = "x", ["date"] = "2026-11-20" })).IsError);
        Assert.True((await mcp.CallToolAsync("delete_quiz", new Dictionary<string, object?> { ["quizId"] = "does-not-exist" })).IsError);

        // Nor see, change or remove A's child and its phones
        Assert.True((await mcp.CallToolAsync("update_child", new Dictionary<string, object?> { ["childId"] = child.Id, ["name"] = "Mallory" })).IsError);
        Assert.True((await mcp.CallToolAsync("set_child_pin", new Dictionary<string, object?> { ["childId"] = child.Id, ["pin"] = "0000" })).IsError);
        Assert.True((await mcp.CallToolAsync("create_pairing_link", new Dictionary<string, object?> { ["childId"] = child.Id })).IsError);
        Assert.True((await mcp.CallToolAsync("list_devices", new Dictionary<string, object?> { ["childId"] = child.Id })).IsError);
        Assert.True((await mcp.CallToolAsync("delete_child", new Dictionary<string, object?> { ["childId"] = child.Id })).IsError);
        Assert.Equal(child.Name, Assert.Single(await LernkickerFactory.Json<List<ChildOverview>>(await a.GetAsync("/api/children"))).Name);

        // Nor revoke A's key
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"/api/api-keys/{keyA.Key.Id}")).StatusCode);
        Assert.Empty(await LernkickerFactory.Json<List<AssignmentOverview>>(await a.GetAsync($"/api/children/{child.Id}/assignments")));
        Assert.Single(await LernkickerFactory.Json<List<QuizOverview>>(await a.GetAsync("/api/quizzes")));
    }

    private static async Task<ApiKeyCreatedResponse> CreateKeyAsync(HttpClient parent, string name = "Assistant", bool readOnly = false) =>
        await LernkickerFactory.Json<ApiKeyCreatedResponse>(await parent.PostAsJsonAsync("/api/api-keys", new NewApiKey(name, readOnly)));

    private async Task<McpClient> ConnectAsync(string token)
    {
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri("https://localhost/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
                AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
            },
            factory.CreateClient(),
            ownsHttpClient: true);
        return await McpClient.CreateAsync(transport);
    }

    private async Task<HttpResponseMessage> PostInitializeAsync(string? token)
    {
        var http = factory.CreateClient();
        if (token is not null)
        {
            http.DefaultRequestHeaders.Authorization = new("Bearer", token);
        }

        return await http.PostAsync("/mcp", InitializeBody());
    }

    private static StringContent InitializeBody()
    {
        var content = new StringContent(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"1"}}}""",
            System.Text.Encoding.UTF8,
            "application/json");
        return content;
    }

    private static async Task<T> CallAsync<T>(McpClient mcp, string tool, Dictionary<string, object?>? arguments = null)
    {
        var result = await mcp.CallToolAsync(tool, arguments ?? []);
        Assert.False(result.IsError ?? false, Text(result));
        var text = Text(result);
        return typeof(T) == typeof(string) ? (T)(object)text : JsonSerializer.Deserialize<T>(text, ApiJson.Options)!;
    }

    private static string Text(CallToolResult result) => string.Concat(result.Content.OfType<TextContentBlock>().Select(t => t.Text));
}
