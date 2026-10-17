using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace InfiniteLiteToken.Core.Models;

/// <summary>
/// Represents a managed API key with DPAPI at-rest encryption, quota limits, and usage metrics.
/// </summary>
public class ApiKeyEntry
{
    [System.Text.Json.Serialization.JsonIgnore]
    public int ActiveModelsCount => ValidatedCodingModels?.Count ?? 0;

    public System.Collections.Generic.List<string> ValidatedCodingModels { get; set; } = new();

    public Guid Id { get; set; } = Guid.NewGuid();

    public ProviderType Provider { get; set; } = ProviderType.GoogleGemini;

    public string Nickname { get; set; } = string.Empty;

    /// <summary>
    /// Base64-encoded DPAPI encrypted ciphertext persisted in storage.
    /// </summary>
    [JsonPropertyName("encryptedApiKey")]
    public string EncryptedApiKey { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public int RequestsToday { get; set; }

    public DateTime? LastRateLimitedAt { get; set; }

    public KeyHealthStatus HealthStatus { get; set; } = KeyHealthStatus.Healthy;

    // Quota and Telemetry Properties
    public long PromptTokensUsed { get; set; }

    public long CompletionTokensUsed { get; set; }

    public long TotalTokensUsed { get; set; }

    public long DailyTokenQuotaLimit { get; set; } = 1_000_000;

    public int DailyRequestLimit { get; set; } = 1500;

    public int TotalRequestsSucceeded { get; set; }

    public int TotalRequestsFailed { get; set; }

    public string? LastUsedModel { get; set; }

    public string? LastErrorMessage { get; set; }

    public double AverageLatencyMs { get; set; }

    /// <summary>
    /// Gets or sets the plaintext API key. The getter decrypts from DPAPI storage,
    /// and the setter encrypts into DPAPI storage on Windows (CurrentUser scope).
    /// </summary>
    [JsonIgnore]
    public string ApiKey
    {
        get
        {
            if (string.IsNullOrEmpty(EncryptedApiKey))
            {
                return string.Empty;
            }

            try
            {
                byte[] cipherBytes = Convert.FromBase64String(EncryptedApiKey);
                byte[] plainBytes = ProtectedData.Unprotect(cipherBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                return string.Empty;
            }
        }
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                EncryptedApiKey = string.Empty;
                return;
            }

            byte[] plainBytes = Encoding.UTF8.GetBytes(value);
            byte[] cipherBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            EncryptedApiKey = Convert.ToBase64String(cipherBytes);
        }
    }
}
