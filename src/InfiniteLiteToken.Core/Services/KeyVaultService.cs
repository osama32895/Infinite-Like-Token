using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using InfiniteLiteToken.Core.Models;

namespace InfiniteLiteToken.Core.Services;

/// <summary>
/// Manages secure persistence, bulk import, and validation probes for API keys.
/// </summary>
public class KeyVaultService
{
    public event System.Action? KeysUpdated;
    public void NotifyKeysChanged() => KeysUpdated?.Invoke();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _vaultFilePath;
    private readonly HttpClient _httpClient;

    public KeyVaultService(string? customVaultPath = null, HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (string.IsNullOrWhiteSpace(customVaultPath))
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appFolder = Path.Combine(localAppData, "InfiniteLiteToken");
            _vaultFilePath = Path.Combine(appFolder, "vault.json");
        }
        else
        {
            _vaultFilePath = customVaultPath;
        }
    }

    public string VaultFilePath => _vaultFilePath;

    /// <summary>
    /// Loads and decrypts API key entries from the DPAPI encrypted vault file.
    /// </summary>
    public async Task<List<ApiKeyEntry>> LoadKeysAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_vaultFilePath))
        {
            return new List<ApiKeyEntry>();
        }

        try
        {
            byte[] encryptedBytes = await File.ReadAllBytesAsync(_vaultFilePath, cancellationToken);
            if (encryptedBytes.Length == 0)
            {
                return new List<ApiKeyEntry>();
            }

            byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
            string json = Encoding.UTF8.GetString(plainBytes);
            List<ApiKeyEntry>? entries = JsonSerializer.Deserialize<List<ApiKeyEntry>>(json, JsonOptions);
            return entries ?? new List<ApiKeyEntry>();
        }
        catch
        {
            return new List<ApiKeyEntry>();
        }
    }

    /// <summary>
    /// Encrypts and writes API key entries to the DPAPI encrypted vault file.
    /// </summary>
    public async Task SaveKeysAsync(IEnumerable<ApiKeyEntry> entries, CancellationToken cancellationToken = default)
    {
        string? directory = Path.GetDirectoryName(_vaultFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(entries, JsonOptions);
        byte[] plainBytes = Encoding.UTF8.GetBytes(json);
        byte[] encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);

        await File.WriteAllBytesAsync(_vaultFilePath, encryptedBytes, cancellationToken);
    }

    /// <summary>
    /// Imports multiple keys pasted line-by-line (e.g. 12 Gemini keys or mixed tokens).
    /// Strips quotes, commas, and trims whitespace.
    /// </summary>
    public IReadOnlyList<ApiKeyEntry> BulkImportKeys(string rawLines, ProviderType provider, string nicknamePrefix = "Key")
    {
        if (string.IsNullOrWhiteSpace(rawLines))
        {
            return Array.Empty<ApiKeyEntry>();
        }

        string[] lines = rawLines.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var results = new List<ApiKeyEntry>();
        int index = 1;

        foreach (string rawLine in lines)
        {
            string cleaned = rawLine.Trim().Trim('"', '\'', ',', ';');
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                continue;
            }

            var entry = new ApiKeyEntry
            {
                Provider = provider,
                Nickname = $"{nicknamePrefix} #{index++}",
                ApiKey = cleaned,
                IsEnabled = true,
                HealthStatus = KeyHealthStatus.Healthy
            };

            results.Add(entry);
        }

        return results;
    }

    /// <summary>
    /// Pings the provider's models endpoint to verify that the key works and returns available models.
    /// </summary>
    public async Task<ValidationResult> ValidateKeyAsync(ApiKeyEntry entry, CancellationToken cancellationToken = default)
    {
        string rawKey = entry.ApiKey;
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            return new ValidationResult(false, "API key is empty.", Array.Empty<string>());
        }

        try
        {
            using var request = BuildValidationRequest(entry.Provider, rawKey);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new ValidationResult(
                    false,
                    $"Provider returned HTTP {(int)response.StatusCode} ({response.StatusCode}): {Truncate(responseContent, 300)}",
                    Array.Empty<string>()
                );
            }

            var models = ParseModelsFromResponse(entry.Provider, responseContent);
            return new ValidationResult(true, null, models);
        }
        catch (Exception ex)
        {
            return new ValidationResult(false, $"Connection error: {ex.Message}", Array.Empty<string>());
        }
    }

    private static HttpRequestMessage BuildValidationRequest(ProviderType provider, string apiKey)
    {
        return provider switch
        {
            ProviderType.GoogleGemini =>
                new HttpRequestMessage(HttpMethod.Get, $"https://generativelanguage.googleapis.com/v1beta/models?key={Uri.EscapeDataString(apiKey)}"),

            ProviderType.NvidiaNim =>
                CreateBearerRequest("https://integrate.api.nvidia.com/v1/models", apiKey),

            ProviderType.OpenRouter =>
                CreateBearerRequest("https://openrouter.ai/api/v1/models", apiKey),

            ProviderType.AtlasCloud =>
                  CreateBearerRequest("https://api.atlascloud.ai/v1/models", apiKey),

              ProviderType.Groq =>
                CreateBearerRequest("https://api.groq.com/openai/v1/models", apiKey),

            ProviderType.Cerebras =>
                CreateBearerRequest("https://api.cerebras.ai/v1/models", apiKey),

            ProviderType.GitHubModels =>
                CreateBearerRequest("https://models.inference.ai.azure.com/models", apiKey),

            ProviderType.CustomOpenAI =>
                CreateBearerRequest("https://api.openai.com/v1/models", apiKey),

            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported provider type.")
        };
    }

    private static HttpRequestMessage CreateBearerRequest(string url, string token)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return req;
    }

    private static IReadOnlyList<string> ParseModelsFromResponse(ProviderType provider, string json)
    {
        var modelList = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (provider == ProviderType.GoogleGemini && root.TryGetProperty("models", out var geminiModels))
            {
                foreach (var m in geminiModels.EnumerateArray())
                {
                    if (m.TryGetProperty("name", out var nameProp))
                    {
                        string? name = nameProp.GetString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            modelList.Add(name.Replace("models/", string.Empty));
                        }
                    }
                }
            }
            else if (root.TryGetProperty("data", out var dataArray))
            {
                foreach (var item in dataArray.EnumerateArray())
                {
                    if (item.TryGetProperty("id", out var idProp))
                    {
                        string? id = idProp.GetString();
                        if (!string.IsNullOrEmpty(id))
                        {
                            modelList.Add(id);
                        }
                    }
                }
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    if (item.TryGetProperty("id", out var idProp))
                    {
                        string? id = idProp.GetString();
                        if (!string.IsNullOrEmpty(id))
                        {
                            modelList.Add(id);
                        }
                    }
                    else if (item.TryGetProperty("name", out var nameProp))
                    {
                        string? name = nameProp.GetString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            modelList.Add(name);
                        }
                    }
                }
            }
        }
        catch
        {
        }

        return modelList.Distinct().OrderBy(m => m).ToList();
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
        {
            return text;
        }
        return text[..maxLength] + "...";
    }
}
