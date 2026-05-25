using Microsoft.AspNetCore.SignalR.Client;
using PasswordBreaker.Shared.Cryptography;
using PasswordBreaker.Shared.Models;
using PasswordBreaker.Shared.Utils;
using System.Diagnostics;

namespace PasswordBreaker.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private HubConnection? _hubConnection;
    private CancellationTokenSource _attackCts = new();

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl("http://localhost:15000/workerHub")
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<string>("PasswordFound", (password) =>
        {
            _logger.LogInformation($"[Global] Password found: {password}. Cancelling current work.");
            _attackCts.Cancel();
        });

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_hubConnection.State == HubConnectionState.Disconnected)
                {
                    _logger.LogInformation("Connecting to server...");
                    await _hubConnection.StartAsync(stoppingToken);
                    _logger.LogInformation("Connected.");
                }

                // Request work
                var chunk = await _hubConnection.InvokeAsync<WorkChunk?>("RequestWork", cancellationToken: stoppingToken);

                if (chunk != null)
                {
                    _logger.LogInformation($"Received chunk: {chunk.ChunkId} for hash {chunk.TargetHash}. Range: {chunk.StartIndex}-{chunk.EndIndex}");
                    
                    if (_attackCts.IsCancellationRequested)
                    {
                        _attackCts = new CancellationTokenSource(); // reset for new attack
                    }

                    // Process chunk
                    await ProcessChunkAsync(chunk, stoppingToken);
                }
                else
                {
                    // No work available, delay before asking again
                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error communicating with server.");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task ProcessChunkAsync(WorkChunk chunk, CancellationToken stoppingToken)
    {
        IHashAlgorithm algorithm = chunk.HashType.ToUpperInvariant() switch
        {
            "MD5" => new Md5HashAlgorithm(),
            "SHA256" => new Sha256HashAlgorithm(),
            "ARGON2" => new Argon2HashAlgorithm(),
            _ => throw new NotSupportedException($"Hash type {chunk.HashType} not supported.")
        };

        bool found = false;
        string? foundPassword = null;
        long hashesComputed = 0;

        long localHashesSinceLastReport = 0;

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, _attackCts.Token);
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = linkedCts.Token,
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            Parallel.For(chunk.StartIndex, chunk.EndIndex + 1, parallelOptions, (index, state) =>
            {
                string attempt = BruteForceGenerator.GenerateStringAtIndex(index, chunk.Alphabet, chunk.MaxLength);
                
                if (string.IsNullOrEmpty(attempt)) return;

                long currentCount = Interlocked.Increment(ref hashesComputed);
                long localCount = Interlocked.Increment(ref localHashesSinceLastReport);

                // Report every 50k hashes or so to the server to keep UI lively
                if (localCount >= 50_000)
                {
                    long toReport = Interlocked.Exchange(ref localHashesSinceLastReport, 0);
                    if (toReport > 0 && _hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
                    {
                        // Fire and forget
                        _ = _hubConnection.InvokeAsync("ReportProgress", toReport, stoppingToken);
                    }
                }

                if (algorithm.VerifyHash(attempt, chunk.TargetHash))
                {
                    found = true;
                    foundPassword = attempt;
                    state.Stop(); // Stop other threads in this parallel loop
                }
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Processing cancelled (password found elsewhere).");
        }
        
        stopwatch.Stop();

        // Report remaining hashes not yet reported
        long remainingToReport = Interlocked.Exchange(ref localHashesSinceLastReport, 0);

        if (found)
        {
            _logger.LogInformation($"[SUCCESS] Password found: {foundPassword} in {stopwatch.ElapsedMilliseconds}ms");
        }
        else
        {
            _logger.LogInformation($"Chunk {chunk.ChunkId} finished. Hashes: {hashesComputed} in {stopwatch.ElapsedMilliseconds}ms");
        }

        // Report final result for chunk (we pass 0 for hashes computed to avoid double counting if we use ReportProgress, but since ReportResult expects total for chunk, we need to pass just the remaining)
        if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
        {
            await _hubConnection.InvokeAsync("ReportResult", found, foundPassword, remainingToReport, stoppingToken);
        }
    }
}
