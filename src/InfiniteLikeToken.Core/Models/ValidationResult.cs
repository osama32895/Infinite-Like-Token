using System.Collections.Generic;

namespace InfiniteLikeToken.Core.Models;

/// <summary>
/// Result of an API key validation probe against a provider's models endpoint.
/// </summary>
public record ValidationResult(
    bool IsValid,
    string? ErrorMessage,
    IReadOnlyList<string> AvailableModels
);
