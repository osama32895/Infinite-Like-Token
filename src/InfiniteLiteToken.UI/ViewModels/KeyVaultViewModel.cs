using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InfiniteLiteToken.Core.Models;
using InfiniteLiteToken.Core.Services;

namespace InfiniteLiteToken.UI.ViewModels;

public partial class KeyVaultViewModel : ObservableObject
{
    private readonly KeyVaultService _vaultService;
    private readonly ProxyProcessManager _proxyManager;

    [ObservableProperty]
    private ObservableCollection<ApiKeyEntry> _keys = new();

    [ObservableProperty]
    private ProviderType _selectedProvider = ProviderType.GoogleGemini;

    [ObservableProperty]
    private string _bulkKeysText = string.Empty;

    [ObservableProperty]
    private bool _isBulkModalOpen;

    [ObservableProperty]
    private string _statusNotification = "Manage your free API keys for the infinite pool.";

    public KeyVaultViewModel(KeyVaultService vaultService, ProxyProcessManager proxyManager)
    {
        _vaultService = vaultService;
        _proxyManager = proxyManager;

        _proxyManager.KeyStatusUpdated += OnKeyStatusUpdated;
        _ = LoadKeysAsync();
    }

    private void OnKeyStatusUpdated(string nickname, KeyHealthStatus status)
    {
        App.Current?.Dispatcher?.Invoke(() =>
        {
            var key = Keys.FirstOrDefault(k => k.Nickname.Equals(nickname, StringComparison.OrdinalIgnoreCase));
            if (key != null)
            {
                key.HealthStatus = status;
                if (status == KeyHealthStatus.Cooldown)
                {
                    key.LastRateLimitedAt = DateTime.Now;
                }
            }
        });
    }

    [RelayCommand]
    public async Task LoadKeysAsync()
    {
        var loaded = await _vaultService.LoadKeysAsync();
        Keys.Clear();
        foreach (var k in loaded)
        {
            Keys.Add(k);
        }
        StatusNotification = $"Loaded {Keys.Count} keys from vault.";
    }

    [RelayCommand]
    public async Task SaveKeysAsync()
    {
        await _vaultService.SaveKeysAsync(Keys);
        _vaultService.NotifyKeysChanged();
        StatusNotification = "Keys saved and encrypted with Windows DPAPI.";
    }

    [RelayCommand]
    public void OpenGetFreeKeyLink()
    {
        string url = SelectedProvider switch
        {
            ProviderType.GoogleGemini => "https://aistudio.google.com/app/apikey",
            ProviderType.Groq => "https://console.groq.com/keys",
            ProviderType.NvidiaNim => "https://build.nvidia.com/",
            ProviderType.OpenRouter => "https://openrouter.ai/keys",
            ProviderType.Cerebras => "https://cloud.cerebras.ai/",
            ProviderType.GitHubModels => "https://github.com/marketplace/models",
            _ => "https://platform.openai.com/api-keys"
        };

        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }

    [RelayCommand]
    public void ShowBulkModal()
    {
        BulkKeysText = string.Empty;
        IsBulkModalOpen = true;
    }

    [RelayCommand]
    public void CloseBulkModal()
    {
        IsBulkModalOpen = false;
    }

    [RelayCommand]
    public async Task ConfirmBulkImportAsync()
    {
        var imported = _vaultService.BulkImportKeys(BulkKeysText, SelectedProvider, SelectedProvider.ToString());
        foreach (var entry in imported)
        {
            Keys.Add(entry);
        }

        await _vaultService.SaveKeysAsync(Keys);
        _vaultService.NotifyKeysChanged();
        IsBulkModalOpen = false;
        StatusNotification = $"Imported {imported.Count} keys successfully!";
    }

    [RelayCommand]
    public async Task TestAllKeysAsync()
    {
        StatusNotification = "Validating all keys against provider endpoints...";
        int validCount = 0;

        foreach (var key in Keys)
        {
            var res = await _vaultService.ValidateKeyAsync(key);
            if (res.IsValid)
            {
                key.HealthStatus = KeyHealthStatus.Healthy;
                                validCount++;
                
                if (key.ValidatedCodingModels == null) key.ValidatedCodingModels = new();
                key.ValidatedCodingModels.Clear();
                
                if (key.Provider.ToString() == "GoogleGemini")
                {
                    // Force the 5 specific Gemini models already mapped in your catalog
                    var meta = InfiniteLiteToken.Core.Models.ProviderCatalog.GetMetadata(key.Provider);
                    if (meta.RecommendedProModels != null) key.ValidatedCodingModels.AddRange(meta.RecommendedProModels);
                    if (meta.RecommendedFlashModels != null) key.ValidatedCodingModels.AddRange(meta.RecommendedFlashModels);
                    if (meta.RecommendedLiteModels != null) key.ValidatedCodingModels.AddRange(meta.RecommendedLiteModels);
                    if (meta.RecommendedReaModels != null) key.ValidatedCodingModels.AddRange(meta.RecommendedReaModels);
                }
                else if (res.AvailableModels != null)
                {
                    // Smart filter for all other APIs (Groq, Together, OpenRouter, etc.)
                    var includeKeywords = new[] { "code", "instruct", "pro", "chat", "flash", "sonnet", "opus", "haiku", "llama", "qwen", "mistral", "deepseek", "phi", "gemma" };
                    var excludeKeywords = new[] { "embed", "vision", "audio", "whisper", "guard", "classify", "moderation", "tts", "image", "stt" };
                    
                    foreach(var m in res.AvailableModels)
                    {
                        bool isCoding = false;
                        foreach(var kw in includeKeywords) { if (m.IndexOf(kw, System.StringComparison.OrdinalIgnoreCase) >= 0) isCoding = true; }
                        
                        bool isExcluded = false;
                        foreach(var kw in excludeKeywords) { if (m.IndexOf(kw, System.StringComparison.OrdinalIgnoreCase) >= 0) isExcluded = true; }
                        
                        if (isCoding && !isExcluded)
                        {
                            key.ValidatedCodingModels.Add(m);
                        }
                    }
                    // Fallback just in case the filter is too aggressive
                    if (key.ValidatedCodingModels.Count == 0 && res.AvailableModels != null) key.ValidatedCodingModels.AddRange(res.AvailableModels);

                // Force WPF DataGrid to re-read ActiveModelsCount safely without breaking the foreach loop
                key.ValidatedCodingModels = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Distinct(key.ValidatedCodingModels));

                System.Windows.Application.Current?.Dispatcher?.InvokeAsync(() => {
                    System.Windows.Data.CollectionViewSource.GetDefaultView(Keys).Refresh();
                });
                
                // Permanently save the newly mapped models to the local database
                _ = _vaultService.SaveKeysAsync(System.Linq.Enumerable.ToList(Keys), default);
                }
            }
            else
            {
                key.HealthStatus = KeyHealthStatus.Invalid;
            }
        }

        await _vaultService.SaveKeysAsync(Keys);
        _vaultService.NotifyKeysChanged();
        StatusNotification = $"Validation complete: {validCount}/{Keys.Count} keys are operational.";
    }

    [RelayCommand]
    public async Task RemoveKeyAsync(ApiKeyEntry key)
    {
        Keys.Remove(key);
        await _vaultService.SaveKeysAsync(Keys);
        _vaultService.NotifyKeysChanged();
        StatusNotification = "Key removed from vault.";
    }
}
