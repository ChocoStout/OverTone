using OverTone.Extensions.DependencyInjection;
using OverTone.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Razor components with interactive server rendering (button clicks run extraction on the server).
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register OverTone via its DI package — the page injects PaletteGenerator.
builder.Services.AddOverTone();

var app = builder.Build();

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
