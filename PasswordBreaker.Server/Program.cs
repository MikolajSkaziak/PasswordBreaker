using PasswordBreaker.Server.Hubs;
using PasswordBreaker.Server.Services;
using PasswordBreaker.Server.Data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

// Set QuestPDF license (Community for personal/small projects)
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Database configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=localhost;Database=passwordbreaker;Username=postgres;Password=postgres";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000") // React origins
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddSignalR();
builder.Services.AddSingleton<WorkQueueManager>();
builder.Services.AddSingleton<WorkerProcessManager>();

var app = builder.Build();

// Automatically apply migrations/ensure created on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // Wait for DB to be ready in Docker
    int retries = 0;
    while (retries < 10)
    {
        try { db.Database.EnsureCreated(); break; }
        catch { retries++; Thread.Sleep(2000); }
    }
}

// Ensure workers are cleaned up when server stops
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    var workerManager = app.Services.GetRequiredService<WorkerProcessManager>();
    workerManager.StopAll();
});

app.UseCors();

// API endpoint to start the attack from Dashboard
app.MapPost("/api/attack/start", async (WorkQueueManager manager, StartAttackRequest req) =>
{
    await manager.StartAttackAsync(req.TargetHash, req.HashType, req.Alphabet, req.MaxLength);
    return Results.Ok();
});

// API endpoint to stop the attack
app.MapPost("/api/attack/stop", async (WorkQueueManager manager) =>
{
    await manager.StopAttackAsync();
    return Results.Ok();
});

// API endpoint to get current status
app.MapGet("/api/attack/status", (WorkQueueManager manager) =>
{
    return Results.Ok(manager.CurrentStatus);
});

// History endpoint
app.MapGet("/api/history", async (AppDbContext db) =>
{
    var history = await db.CrackedPasswords
        .OrderByDescending(x => x.CrackedAt)
        .Take(50)
        .ToListAsync();
    return Results.Ok(history);
});

app.MapGet("/api/history/{id:int}/report", async (int id, AppDbContext db) =>
{
    var item = await db.CrackedPasswords.FindAsync(id);
    if (item == null) return Results.NotFound();

    var document = Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontSize(12).FontFamily(Fonts.Verdana));

            page.Header().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("PASSWORD BREAKER").FontSize(24).SemiBold().FontColor(Colors.Blue.Medium);
                    col.Item().Text("Security Audit Report").FontSize(14).Italic().FontColor(Colors.Grey.Medium);
                });

                row.ConstantItem(60).Column(col =>
                {
                    var logoPath = Path.Combine(Directory.GetCurrentDirectory(), "logo.png");
                    if (File.Exists(logoPath))
                    {
                        col.Item().Height(40).Image(logoPath);
                    }
                    else
                    {
                        col.Item().Height(40).Placeholder();
                    }
                });
            });

            page.Content().PaddingVertical(1, Unit.Centimetre).Column(x =>
            {
                x.Spacing(10);

                x.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                x.Item().Row(row =>
                {
                    row.RelativeItem().Text("Target Hash:");
                    row.RelativeItem().Text(item.Hash).FontFamily(Fonts.CourierNew).FontSize(10);
                });

                x.Item().Row(row =>
                {
                    row.RelativeItem().Text("Algorithm:");
                    row.RelativeItem().Text(item.Algorithm).SemiBold();
                });

                x.Item().Row(row =>
                {
                    row.RelativeItem().Text("Resulting Password:");
                    row.RelativeItem().Text(item.Password).FontColor(Colors.Green.Medium).FontSize(16).Bold();
                });

                x.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                x.Item().Row(row =>
                {
                    row.RelativeItem().Text("Workers Used:");
                    row.RelativeItem().Text($"{item.WorkerCount} nodes");
                });

                x.Item().Row(row =>
                {
                    row.RelativeItem().Text("Time Taken:");
                    row.RelativeItem().Text(item.Duration.ToString(@"hh\:mm\:ss\.fff"));
                });

                x.Item().Row(row =>
                {
                    row.RelativeItem().Text("Cracked At:");
                    row.RelativeItem().Text(item.CrackedAt.ToString("g"));
                });
            });

            page.Footer().AlignCenter().Text(x =>
            {
                x.Span("Page ");
                x.CurrentPageNumber();
            });
        });
    });

    var pdfData = document.GeneratePdf();
    return Results.File(pdfData, "application/pdf", $"Report_{item.Hash.Substring(0, 8)}.pdf");
});

// New endpoints for worker management
app.MapPost("/api/workers/count", async (WorkerProcessManager manager, SetWorkerCountRequest req) =>
{
    await manager.SetWorkerCountAsync(req.Count);
    return Results.Ok(new { CurrentCount = manager.GetWorkerCount() });
});

app.MapGet("/api/workers/count", (WorkerProcessManager manager) =>
{
    return Results.Ok(new { CurrentCount = manager.GetWorkerCount() });
});

app.MapHub<WorkerHub>("/workerHub");
app.MapHub<DashboardHub>("/dashboardHub");

app.Run();

public record StartAttackRequest(string TargetHash, string HashType, string Alphabet, int MaxLength);
public record SetWorkerCountRequest(int Count);
