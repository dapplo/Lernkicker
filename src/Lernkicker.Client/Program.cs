using Lernkicker.Client;
using Lernkicker.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The culture is chosen in js/start.js before Blazor starts (stored choice, else the browser's language)
builder.Services.AddLocalization();
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<Api>();
builder.Services.AddScoped<Display>();
builder.Services.AddScoped<UiLanguage>();

await builder.Build().RunAsync();
