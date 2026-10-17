namespace InfiniteLikeToken.Core.Models;

/// <summary>
/// Operational health status of an API key entry.
/// </summary>
public enum KeyHealthStatus
{
    Healthy,
    Cooldown,
    QuotaExhausted,
    Invalid
}
