using OverTone.Extensions.DependencyInjection;
using OverTone.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Serve framework static assets (e.g. _framework/blazor.web.js) in EVERY environment. Without this,
// `dotnet run` outside Development serves an EMPTY blazor.web.js (HTTP 200, 0 bytes), so the Blazor
// interactive circuit never starts and every button / file input is dead.
builder.WebHost.UseStaticWebAssets();

// Razor components with interactive server rendering (button clicks run extraction on the server).
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Loading images from a URL (server-side download).
builder.Services.AddHttpClient();

// Register OverTone via its DI package — the page injects PaletteGenerator.
builder.Services.AddOverTone();

var app = builder.Build();

app.UseAntiforgery();

// Serves framework static assets, including _framework/blazor.web.js. Without this the interactive
// circuit never boots (the script 404s) and the page is dead — buttons and file input do nothing.
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
