using Lernkicker.Client;
using Lernkicker.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The culture is chosen in js/start.js before Blazor starts (stored choice, else the browser's language)
builder.Services.AddLocalization();
builder.Services.AddScoped<AppUpdate>();
builder.Services.AddScoped(sp => new HttpClient(new BuildCheck(sp.GetRequiredService<AppUpdate>())) { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<Api>();
builder.Services.AddScoped<Display>();
builder.Services.AddScoped<CurrentTheme>();
builder.Services.AddScoped<UiLanguage>();
builder.Services.AddScoped<UploadFiles>();
builder.Services.AddScoped<PlayConnection>();
builder.Services.AddScoped<Speech>();

await builder.Build().RunAsync();
