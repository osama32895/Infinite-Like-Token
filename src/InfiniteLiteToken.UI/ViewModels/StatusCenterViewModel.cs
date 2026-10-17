using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using InfiniteLiteToken.Core.Models;
using InfiniteLiteToken.Core.Services;

namespace InfiniteLiteToken.UI.ViewModels;

public class KeyUsageMetric : ObservableObject
{
    public string Provider { get; set; } = "";
    public string KeyNickname { get; set; } = "";
    public string Status { get; set; } = "";
    public long TokensUsed { get; set; }
    public int RequestsToday { get; set; }
    public long TokenLimit { get; set; }
}

public partial class StatusCenterViewModel : ObservableObject
{
    private readonly KeyVaultService _vaultService;
    private readonly TelemetryService _telemetryService;

    [ObservableProperty] private string _totalTokens = "0";
    [ObservableProperty] private string _activeKeys = "0 / 0 Healthy";
    [ObservableProperty] private string _searchQuery = "";

    private ObservableCollection<KeyUsageMetric> _metrics = new();
    public ICollectionView KeyUsagesView { get; }

    public StatusCenterViewModel(KeyVaultService vaultService, TelemetryService telemetryService)
    {
        
        _vaultService = vaultService;
        _vaultService.KeysUpdated += () => { System.Windows.Application.Current?.Dispatcher?.InvokeAsync(() => _ = RefreshDashboardAsync()); };
        _telemetryService = telemetryService;
        
        KeyUsagesView = CollectionViewSource.GetDefaultView(_metrics);
        KeyUsagesView.Filter = FilterMetrics;

        // Auto-refresh metrics when the router consumes tokens
        _telemetryService.TelemetryUpdated += (record, summary) => 
        {
            System.Windows.Application.Current?.Dispatcher?.InvokeAsync(() => _ = LoadDataAsync());
        };

        _ = LoadDataAsync();
    }

    public async Task LoadDataAsync()
    {
        var keys = await _vaultService.LoadKeysAsync();
        
        _metrics.Clear();
        int healthy = 0;

        foreach (var key in keys)
        {
            if (key.HealthStatus == KeyHealthStatus.Healthy) healthy++;
            _metrics.Add(new KeyUsageMetric 
            { 
                Provider = key.Provider.ToString(), 
                KeyNickname = key.Nickname ?? "Unknown", 
                Status = key.HealthStatus.ToString(), 
                TokensUsed = key.TotalTokensUsed 
            });
        }

        long totalUsed = 0;
        long totalQuota = 0;
        foreach(var k in keys) { totalUsed += k.TotalTokensUsed; totalQuota += k.DailyTokenQuotaLimit; }
        TotalTokens = totalUsed.ToString("N0");
        ActiveKeys = $"{healthy} / {keys.Count} Healthy";
        KeyUsagesView.Refresh();
    }

    partial void OnSearchQueryChanged(string value)
    {
        KeyUsagesView.Refresh();
    }

    private bool FilterMetrics(object obj)
    {
        if (string.IsNullOrWhiteSpace(SearchQuery)) return true;
        if (obj is KeyUsageMetric metric)
        {
            return metric.Provider.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                   metric.KeyNickname.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    public async System.Threading.Tasks.Task RefreshDashboardAsync()
    {
        var keys = await _vaultService.LoadKeysAsync();
        int healthy = 0;
        long totalUsed = 0;
        long totalQuota = 0;
        
        _metrics.Clear();
        foreach (var key in keys)
        {
            if (key.HealthStatus == InfiniteLiteToken.Core.Models.KeyHealthStatus.Healthy) healthy++;
            totalUsed += key.TotalTokensUsed;
            totalQuota += key.DailyTokenQuotaLimit;
            
            _metrics.Add(new KeyUsageMetric
            {
                Provider = key.Provider.ToString(),
                KeyNickname = key.Nickname ?? "Unknown",
                Status = key.HealthStatus.ToString(),
                TokensUsed = key.TotalTokensUsed,
                RequestsToday = key.RequestsToday,
                TokenLimit = key.DailyTokenQuotaLimit
            });
        }

        TotalTokens = totalUsed.ToString("N0");
        ActiveKeys = $"{healthy} / {keys.Count} Healthy";
        KeyUsagesView.Refresh();
    }

}
