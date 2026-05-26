using Microsoft.AspNetCore.SignalR;
using PasswordBreaker.Server.Services;
using PasswordBreaker.Shared.Models;

namespace PasswordBreaker.Server.Hubs;

public class WorkerHub : Hub
{
    private readonly WorkQueueManager _workQueue;

    public WorkerHub(WorkQueueManager workQueue)
    {
        _workQueue = workQueue;
    }

    public async Task<WorkChunk?> RequestWork()
    {
        return _workQueue.GetNextChunk(Context.ConnectionId);
    }

    public async Task ReportResult(bool found, string? password, long hashesComputed)
    {
        await _workQueue.ReportResultAsync(Context.ConnectionId, found, password, hashesComputed);
    }

    public async Task ReportProgress(long hashesComputed)
    {
        await _workQueue.ReportProgressAsync(hashesComputed);
    }

    public override async Task OnConnectedAsync()
    {
        Console.WriteLine($"[SIGNALR] Worker connected: {Context.ConnectionId}");
        await _workQueue.HandleWorkerConnect(Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        Console.WriteLine($"[SIGNALR] Worker disconnected: {Context.ConnectionId}");
        await _workQueue.HandleWorkerDisconnect(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
