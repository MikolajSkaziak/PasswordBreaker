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
        
        // Find the worker project path more robustly
        var currentDir = Directory.GetCurrentDirectory();
        // If we are in the Server bin folder, we need to go up more levels
        _workerProjectPath = FindWorkerProject(currentDir);
    }

    private string FindWorkerProject(string startDir)
    {
        var dir = startDir;
        while (dir != null)
        {
            var potentialPath = Path.Combine(dir, "PasswordBreaker.Worker", "PasswordBreaker.Worker.csproj");
            if (File.Exists(potentialPath)) return potentialPath;
            
            // Also check sibling if we are in PasswordBreaker.Server
            var parent = Directory.GetParent(dir)?.FullName;
            if (parent != null)
            {
                potentialPath = Path.Combine(parent, "PasswordBreaker.Worker", "PasswordBreaker.Worker.csproj");
                if (File.Exists(potentialPath)) return potentialPath;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new FileNotFoundException("Could not find PasswordBreaker.Worker.csproj");
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
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{_workerProjectPath}\" -c Release -- --ServiceUrls:Server http://localhost:15000 --threads 1",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        try
        {
            var process = new Process { StartInfo = startInfo };
            
            // Redirect output to server console so user can see what's happening
            process.OutputDataReceived += (s, e) => { if (e.Data != null) Console.WriteLine($"[Worker {process.Id}] {e.Data}"); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) Console.Error.WriteLine($"[Worker {process.Id} ERROR] {e.Data}"); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            _processes.Add(process);
            _logger.LogInformation($"[MANAGER] Spawned worker PID: {process.Id}");
            Console.WriteLine($"[SERVER] Worker process started (PID: {process.Id}). Logs will be prefixed with [Worker {process.Id}]");
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
