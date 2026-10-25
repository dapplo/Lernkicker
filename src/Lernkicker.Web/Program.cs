using System.Threading.RateLimiting;
using Lernkicker.Application.Ports;
using Lernkicker.Application.UseCases;
using Lernkicker.Calendar;
using Lernkicker.Contracts;
using Lernkicker.Persistence;
using Lernkicker.QuizFormat;
using Lernkicker.Telegram;
using Lernkicker.Web;
using Lernkicker.Web.Endpoints;
using Lernkicker.Web.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

var dataPath = Path.GetFullPath(builder.Configuration["Lernkicker:DataPath"] ?? "data");
Directory.CreateDirectory(dataPath);

// Adapters
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IFamilyScope, HttpFamilyScope>();
builder.Services.AddSingleton<IQuizFormat, QuizJson>();
builder.Services.AddSingleton<ILessonFormat, LessonJson>();

// Calendars: dates and times are in the family's time zone (one per installation)
var timeZone = builder.Configuration["Lernkicker:TimeZone"] ?? "Europe/Berlin";
if (!ZonedClock.IsKnown(timeZone))
{
    throw new InvalidOperationException($"Unknown time zone in Lernkicker:TimeZone: {timeZone}");
}

builder.Services.AddSingleton<ICalendarSource>(_ => new IcsCalendarSource(timeZone));
builder.Services.AddSingleton<ICalendarWriter>(_ => new IcsCalendarWriter(timeZone));
builder.Services.AddSingleton<ILocalClock>(sp => new ZonedClock(sp.GetRequiredService<TimeProvider>(), timeZone));
builder.Services.AddHostedService<CalendarSync>();
builder.Services.AddTelegram(builder.Configuration);
builder.Services.AddPersistence(builder.Configuration.GetConnectionString("Lernkicker") ?? $"Data Source={Path.Combine(dataPath, "lernkicker.db")}");

// Use cases
builder.Services.AddScoped<AdminUseCases>();
builder.Services.AddScoped<AccountUseCases>();
builder.Services.AddScoped<QuizLibraryUseCases>();
builder.Services.AddScoped<ChildrenUseCases>();
builder.Services.AddScoped<ChildDeviceUseCases>();
builder.Services.AddScoped<PracticeUseCases>();
builder.Services.AddScoped<CalendarUseCases>();
builder.Services.AddScoped<LessonUseCases>();
builder.Services.AddScoped<SharedLibraryUseCases>();
builder.Services.AddScoped<TelegramUseCases>();
builder.Services.AddScoped<TelegramBotUseCases>();
builder.Services.AddSingleton<ISecretProtector, DataProtectionSecrets>();

// Keys for cookies survive restarts and image updates
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataPath, "keys")))
    .SetApplicationName("Lernkicker");

builder.Services.AddIdentity<ParentUser, IdentityRole<Guid>>(o =>
    {
        o.User.RequireUniqueEmail = true;
        o.Password.RequiredLength = 12;
        o.Password.RequireNonAlphanumeric = false;
        o.Password.RequireUppercase = false;
        o.Password.RequireLowercase = false;
        o.Password.RequireDigit = false;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        o.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<LernkickerDbContext>()
    .AddClaimsPrincipalFactory<ParentClaimsFactory>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = Cookies.Parent;
    ApiCookie(o);
    o.ExpireTimeSpan = TimeSpan.FromDays(30);
});

builder.Services.AddAuthentication()
    .AddCookie(Schemes.Child, o =>
    {
        o.Cookie.Name = Cookies.Child;
        ApiCookie(o);
        o.ExpireTimeSpan = TimeSpan.FromDays(30);
        // A removed or locked device ends the child's session immediately
        o.Events.OnValidatePrincipal = async context =>
        {
            var deviceId = Guid.TryParse(context.Principal?.FindFirst(Claims.DeviceId)?.Value, out var id) ? id : Guid.Empty;
            var devices = context.HttpContext.RequestServices.GetRequiredService<ChildDeviceUseCases>();
            if (!await devices.IsDeviceActiveAsync(deviceId))
            {
                context.RejectPrincipal();
            }
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.Parent, p => p
        .AddAuthenticationSchemes(IdentityConstants.ApplicationScheme)
        .RequireAuthenticatedUser()
        .RequireClaim(Claims.FamilyId))
    .AddPolicy(Policies.Admin, p => p
        .AddAuthenticationSchemes(IdentityConstants.ApplicationScheme)
        .RequireAuthenticatedUser()
        .RequireRole(Roles.Admin))
    .AddPolicy(Policies.Child, p => p
        .AddAuthenticationSchemes(Schemes.Child)
        .RequireAuthenticatedUser()
        .RequireClaim(Claims.ChildId));

var loginsPerMinute = builder.Configuration.GetValue("Lernkicker:Limits:LoginsPerMinute", 10);
var childRequestsPerMinute = builder.Configuration.GetValue("Lernkicker:Limits:ChildLoginsPerMinute", 20);
var calendarRequestsPerMinute = builder.Configuration.GetValue("Lernkicker:Limits:CalendarRequestsPerMinute", 30);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(RateLimits.Login, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "?",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = loginsPerMinute, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy(RateLimits.Child, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "?",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = childRequestsPerMinute, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy(RateLimits.Calendar, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "?",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = calendarRequestsPerMinute, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.AllowOutOfOrderMetadataProperties = ApiJson.Options.AllowOutOfOrderMetadataProperties;
    o.SerializerOptions.DefaultIgnoreCondition = ApiJson.Options.DefaultIgnoreCondition;
});

// Tailscale serve/funnel proxies from localhost and passes the client address on. A proxy in another
// container (e.g. TSDProxy) has to be trusted explicitly: Lernkicker:TrustedProxies = "172.16.0.0/12"
var trustedProxies = (builder.Configuration["Lernkicker:TrustedProxies"] ?? "")
    .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
    .Select(System.Net.IPNetwork.Parse)
    .ToList();
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    trustedProxies.ForEach(o.KnownIPNetworks.Add);
});

// The address people use (e.g. https://lernkicker.example.ts.net). Links in QR codes, invitations and the
// calendar feed are built from it; without it from the request, which needs the proxy's X-Forwarded-Proto.
var publicUrl = builder.Configuration["Lernkicker:PublicUrl"] is { Length: > 0 } configuredUrl
    ? Uri.TryCreate(configuredUrl, UriKind.Absolute, out var parsedUrl) && parsedUrl.Scheme is "https" or "http"
        ? parsedUrl
        : throw new InvalidOperationException($"Lernkicker:PublicUrl is not an http(s) URL: {configuredUrl}")
    : null;

var app = builder.Build();

app.UseForwardedHeaders();
if (publicUrl is not null)
{
    app.Use((context, next) =>
    {
        context.Request.Scheme = publicUrl.Scheme;
        context.Request.Host = new HostString(publicUrl.Authority);
        return next(context);
    });
}

app.UseSecurityHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/api/status", () => Results.Ok(new { status = "ok" }));
app.MapAccount();
app.MapAdmin();
app.MapQuizzes();
app.MapChildren();
app.MapDevice();
app.MapCalendar();
app.MapLessons();
app.MapLibrary();
app.Map("/api/{**rest}", ApiResults.NotFound);

// The PWA (Lernkicker.Client). Fingerprinted files are cached forever; every other path is a page of the app.
app.MapStaticAssets();
app.MapFallbackToFile("index.html", new StaticFileOptions
{
    OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache",
});

await PersistenceSetup.InitializeAsync(app.Services, app.Configuration.GetSection("Admin").Get<AdminOptions>() ?? new(), app.Logger);
await app.RunAsync();

static void ApiCookie(CookieAuthenticationOptions o)
{
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.SlidingExpiration = true;
    // An API answers 401/403 instead of redirecting to a login page
    o.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    o.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
}

public partial class Program;
