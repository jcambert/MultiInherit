using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using MultiInherit.Todo.Components;
using MultiInherit.Todo.Data;
using MultiInherit.Todo.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Blazor ────────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ── MudBlazor ─────────────────────────────────────────────────────────────
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = MudBlazor.Defaults.Classes.Position.BottomRight;
    config.SnackbarConfiguration.MaxDisplayedSnackbars = 5;
});

// ── Entity Framework Core + PostgreSQL ────────────────────────────────────
builder.Services.AddDbContextFactory<TodoDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        b => b.MigrationsAssembly("MultiInherit.Todo")));

// ── Application services ──────────────────────────────────────────────────
builder.Services.AddScoped<ITodoService, TodoService>();
builder.Services.AddSingleton<TodoEventService>();

// ── Hosted background service ──────────────────────────────────────────────
builder.Services.AddHostedService<DeadlineMonitorService>();

var app = builder.Build();

// ── Database initialisation ───────────────────────────────────────────────
await using (var scope = app.Services.CreateAsyncScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TodoDbContext>>();
    await using var ctx = await factory.CreateDbContextAsync();
    await ctx.Database.EnsureCreatedAsync();
    await TodoSeeder.SeedAsync(ctx);
}

// ── Pipeline ──────────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
