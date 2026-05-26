using PasswordBreaker.Server.Hubs;
using PasswordBreaker.Server.Services;
using PasswordBreaker.Server.Data;
using Microsoft.EntityFrameworkCore;

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
