using PasswordBreaker.Server.Hubs;
using PasswordBreaker.Server.Services;

var builder = WebApplication.CreateBuilder(args);

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

var app = builder.Build();

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

app.MapHub<WorkerHub>("/workerHub");
app.MapHub<DashboardHub>("/dashboardHub");

app.Run();

public record StartAttackRequest(string TargetHash, string HashType, string Alphabet, int MaxLength);
