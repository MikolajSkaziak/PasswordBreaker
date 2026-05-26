using System.Collections.Concurrent;
using PasswordBreaker.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using PasswordBreaker.Server.Hubs;
using PasswordBreaker.Server.Data;

namespace PasswordBreaker.Server.Services;

public class WorkQueueManager
{
    private readonly ConcurrentQueue<WorkChunk> _pendingChunks = new();
    private readonly ConcurrentDictionary<string, WorkChunk> _assignedChunks = new(); // ConnectionId -> Chunk
    private readonly IHubContext<WorkerHub> _workerHub;
    private readonly IHubContext<DashboardHub> _dashboardHub;
    private readonly IServiceScopeFactory _scopeFactory;

    public AttackStatus CurrentStatus { get; private set; } = new();

    public WorkQueueManager(IHubContext<WorkerHub> workerHub, IHubContext<DashboardHub> dashboardHub, IServiceScopeFactory scopeFactory)
    {
        _workerHub = workerHub;
        _dashboardHub = dashboardHub;
        _scopeFactory = scopeFactory;
    }

    public async Task StartAttackAsync(string targetHash, string hashType, string alphabet, int maxLength)
    {
        int workers = CurrentStatus.ConnectedWorkers;
        CurrentStatus = new AttackStatus
        {
            IsActive = true,
            TargetHash = targetHash,
            StartTime = DateTime.UtcNow,
            TotalHashesComputed = 0,
            ConnectedWorkers = workers
        };

        _pendingChunks.Clear();
        _assignedChunks.Clear();

        // Calculate total combinations
        long totalCombinations = 0;
        long currentCount = alphabet.Length;
        for (int i = 1; i <= maxLength; i++)
        {
            totalCombinations += currentCount;
            currentCount *= alphabet.Length;
        }

        // Chunking
        long chunkSize = 1_000_000; // 1M hashes per chunk
        long startIndex = 0;

        while (startIndex < totalCombinations)
        {
            long endIndex = Math.Min(startIndex + chunkSize - 1, totalCombinations - 1);
            _pendingChunks.Enqueue(new WorkChunk
            {
                TargetHash = targetHash,
                HashType = hashType,
                Alphabet = alphabet,
                MaxLength = maxLength,
                StartIndex = startIndex,
                EndIndex = endIndex
            });
            startIndex = endIndex + 1;
        }

        await _dashboardHub.Clients.All.SendAsync("AttackStarted", CurrentStatus);
    }

    public async Task StopAttackAsync()
    {
        if (!CurrentStatus.IsActive) return;

        CurrentStatus.IsActive = false;
        CurrentStatus.EndTime = DateTime.UtcNow;
        _pendingChunks.Clear();
        _assignedChunks.Clear();

        // Broadcast to workers to stop
        await _workerHub.Clients.All.SendAsync("PasswordFound", "[ATTACK STOPPED BY USER]");
        // Broadcast to dashboard
        await _dashboardHub.Clients.All.SendAsync("AttackFinished", CurrentStatus);
    }

    public WorkChunk? GetNextChunk(string connectionId)
    {
        if (!CurrentStatus.IsActive) return null;

        if (_pendingChunks.TryDequeue(out var chunk))
        {
            _assignedChunks[connectionId] = chunk;
            Console.WriteLine($"[QUEUE] Assigned chunk {chunk.StartIndex}-{chunk.EndIndex} to worker {connectionId}");
            return chunk;
        }

        return null;
    }

    public async Task ReportResultAsync(string connectionId, bool found, string? password, long hashesComputed)
    {
        CurrentStatus.TotalHashesComputed += hashesComputed;
        _assignedChunks.TryRemove(connectionId, out var chunk);

        if (found && CurrentStatus.IsActive)
        {
            CurrentStatus.IsActive = false;
            CurrentStatus.FoundPassword = password;
            CurrentStatus.EndTime = DateTime.UtcNow;

            // Save to database
            if (password != null && chunk != null)
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.CrackedPasswords.Add(new CrackedPassword
                {
                    Hash = chunk.TargetHash,
                    Password = password,
                    Algorithm = chunk.HashType,
                    WorkerCount = CurrentStatus.ConnectedWorkers,
                    CrackedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }

            await _workerHub.Clients.All.SendAsync("PasswordFound", password);
            await _dashboardHub.Clients.All.SendAsync("AttackFinished", CurrentStatus);
        }
        else
        {
            await _dashboardHub.Clients.All.SendAsync("StatsUpdated", CurrentStatus.TotalHashesComputed);
        }
    }

    public async Task ReportProgressAsync(long hashesComputed)
    {
        if (CurrentStatus.IsActive)
        {
            CurrentStatus.TotalHashesComputed += hashesComputed;
            await _dashboardHub.Clients.All.SendAsync("StatsUpdated", CurrentStatus.TotalHashesComputed);
        }
    }

    public async Task HandleWorkerDisconnect(string connectionId)
    {
        CurrentStatus.ConnectedWorkers = Math.Max(0, CurrentStatus.ConnectedWorkers - 1);
        await _dashboardHub.Clients.All.SendAsync("WorkerCountUpdated", CurrentStatus.ConnectedWorkers);

        if (_assignedChunks.TryRemove(connectionId, out var chunk))
        {
            // Put the chunk back if the attack is still active
            if (CurrentStatus.IsActive)
            {
                _pendingChunks.Enqueue(chunk);
            }
        }
    }

    public async Task HandleWorkerConnect(string connectionId)
    {
        CurrentStatus.ConnectedWorkers++;
        await _dashboardHub.Clients.All.SendAsync("WorkerCountUpdated", CurrentStatus.ConnectedWorkers);
    }
}
