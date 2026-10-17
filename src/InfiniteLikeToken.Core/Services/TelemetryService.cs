using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using InfiniteLikeToken.Core.Models;

namespace InfiniteLikeToken.Core.Services;

public record TokenConsumptionRecord(
    Guid KeyId,
    string KeyNickname,
    ProviderType Provider,
    string Model,
    long PromptTokens,
    long CompletionTokens,
    long TotalTokens,
    double LatencyMs,
    bool Succeeded,
    DateTime Timestamp
);

public class TelemetrySummary
{
    public long GrandTotalTokens { get; set; }
    public long GrandTotalPromptTokens { get; set; }
    public long GrandTotalCompletionTokens { get; set; }
    public int GrandTotalRequests { get; set; }
    public int GrandTotalSuccesses { get; set; }
    public int GrandTotalFailures { get; set; }
    public Dictionary<string, long> TokensByProvider { get; set; } = new();
    public Dictionary<string, long> TokensByModel { get; set; } = new();
}

public class TelemetryService
{
    private readonly string _telemetryPath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private TelemetrySummary _summary = new();
    private readonly ConcurrentQueue<TokenConsumptionRecord> _recentEvents = new();

    public event Action<TokenConsumptionRecord, TelemetrySummary>? TelemetryUpdated;

    public TelemetryService(string? customPath = null)
    {
        if (string.IsNullOrWhiteSpace(customPath))
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appFolder = Path.Combine(localAppData, "InfiniteLikeToken");
            _telemetryPath = Path.Combine(appFolder, "telemetry.json");
        }
        else
        {
            _telemetryPath = customPath;
        }

        _ = LoadTelemetryAsync();
    }

    public TelemetrySummary CurrentSummary => _summary;
    public IReadOnlyCollection<TokenConsumptionRecord> RecentEvents => _recentEvents.ToArray();

    public async Task RecordUsageAsync(
        ApiKeyEntry key,
        string model,
        long promptTokens,
        long completionTokens,
        double latencyMs,
        bool success,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        long totalTokens = promptTokens + completionTokens;

        // Update key in-memory metrics
        key.PromptTokensUsed += promptTokens;
        key.CompletionTokensUsed += completionTokens;
        key.TotalTokensUsed += totalTokens;
        key.RequestsToday++;
        key.LastUsedModel = model;

        if (success)
        {
            key.TotalRequestsSucceeded++;
            key.HealthStatus = KeyHealthStatus.Healthy;
        }
        else
        {
            key.TotalRequestsFailed++;
            key.LastErrorMessage = errorMessage;
        }

        if (key.AverageLatencyMs <= 0)
        {
            key.AverageLatencyMs = latencyMs;
        }
        else
        {
            key.AverageLatencyMs = (key.AverageLatencyMs * 0.8) + (latencyMs * 0.2);
        }

        var record = new TokenConsumptionRecord(
            key.Id,
            key.Nickname,
            key.Provider,
            model,
            promptTokens,
            completionTokens,
            totalTokens,
            latencyMs,
            success,
            DateTime.Now
        );

        _recentEvents.Enqueue(record);
        while (_recentEvents.Count > 100)
        {
            _recentEvents.TryDequeue(out _);
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            _summary.GrandTotalTokens += totalTokens;
            _summary.GrandTotalPromptTokens += promptTokens;
            _summary.GrandTotalCompletionTokens += completionTokens;
            _summary.GrandTotalRequests++;
            if (success) _summary.GrandTotalSuccesses++;
            else _summary.GrandTotalFailures++;

            string provName = key.Provider.ToString();
            _summary.TokensByProvider[provName] = _summary.TokensByProvider.GetValueOrDefault(provName, 0) + totalTokens;
            _summary.TokensByModel[model] = _summary.TokensByModel.GetValueOrDefault(model, 0) + totalTokens;

            await SaveTelemetryUnsafeAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }

        TelemetryUpdated?.Invoke(record, _summary);
    }

    private async Task LoadTelemetryAsync()
    {
        if (!File.Exists(_telemetryPath)) return;
        try
        {
            await _lock.WaitAsync();
            try
            {
                string json = await File.ReadAllTextAsync(_telemetryPath);
                var loaded = JsonSerializer.Deserialize<TelemetrySummary>(json);
                if (loaded != null) _summary = loaded;
            }
            finally
            {
                _lock.Release();
            }
        }
        catch
        {
        }
    }

    private async Task SaveTelemetryUnsafeAsync(CancellationToken cancellationToken)
    {
        try
        {
            string? dir = Path.GetDirectoryName(_telemetryPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            string json = JsonSerializer.Serialize(_summary, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_telemetryPath, json, cancellationToken);
        }
        catch
        {
        }
    }
}
