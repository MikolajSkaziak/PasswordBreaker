using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;

namespace PasswordBreaker.Server.Services;

public class WorkerProcessManager
{
    private readonly List<Process> _processes = new();
    private readonly string _workerProjectPath;
    private readonly ILogger<WorkerProcessManager> _logger;
    private readonly IHubContext<Hubs.DashboardHub> _dashboardHub;

    public WorkerProcessManager(ILogger<WorkerProcessManager> logger, IHubContext<Hubs.DashboardHub> dashboardHub)
    {
        _logger = logger;
        _dashboardHub = dashboardHub;
        // Path relative to the project directory when running via 'dotnet run'
        _workerProjectPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "PasswordBreaker.Worker", "PasswordBreaker.Worker.csproj"));
    }

    public async Task SetWorkerCountAsync(int count)
    {
        lock (_processes)
        {
            _logger.LogInformation($"Adjusting workers: current {_processes.Count}, target {count}");

            while (_processes.Count < count)
            {
                StartNewWorker();
            }

            while (_processes.Count > count)
            {
                StopOneWorker();
            }
        }

        await _dashboardHub.Clients.All.SendAsync("WorkerCountUpdated", GetWorkerCount());
    }

    public int GetWorkerCount()
    {
        lock (_processes)
        {
            // Clean up exited processes
            _processes.RemoveAll(p => p.HasExited);
            return _processes.Count;
        }
    }

    private void StartNewWorker()
    {
        var workerDir = Path.GetDirectoryName(_workerProjectPath);
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "run -- threads=1 ServiceUrls:Server=http://localhost:80",
            WorkingDirectory = workerDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = false,
            RedirectStandardError = false
        };


        try
        {
            var process = Process.Start(startInfo);
            if (process != null)
            {
                _processes.Add(process);
                _logger.LogInformation($"[MANAGER] Spawned worker PID: {process.Id}. Directory: {workerDir}");
                Console.WriteLine($"[SERVER] Worker process started (PID: {process.Id}). Waiting for connection...");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MANAGER] Failed to start worker process.");
            Console.WriteLine($"[ERROR] Failed to spawn worker: {ex.Message}");
        }
    }

    private void StopOneWorker()
    {
        if (_processes.Count == 0) return;

        var process = _processes[^1];
        _processes.RemoveAt(_processes.Count - 1);

        try
        {
            if (!process.HasExited)
            {
                process.Kill(true); // Kill entire process tree
                _logger.LogInformation($"Terminated worker process. PID: {process.Id}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error terminating worker process.");
        }
    }

    public void StopAll()
    {
        lock (_processes)
        {
            foreach (var process in _processes)
            {
                try { if (!process.HasExited) process.Kill(true); } catch { }
            }
            _processes.Clear();
        }
    }
}
