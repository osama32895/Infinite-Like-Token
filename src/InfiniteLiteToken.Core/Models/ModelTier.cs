namespace InfiniteLiteToken.Core.Models;

/// <summary>
/// Routing and performance tiers for model assignment.
/// </summary>
public enum ModelTier
{
    /// <summary>Ultra-fast low-latency tier for simple edits and casual chat.</summary>
    UltraFastLite,

    /// <summary>Balanced high-throughput tier for everyday coding and review.</summary>
    StandardFlash,

    /// <summary>High-capability reasoning tier for architecture, deep debugging, and complex synthesis.</summary>
    DeepReasoningPro,

    /// <summary>Specialized tier tuned for REA (Reverse Engineering Anything) and tool-heavy agent workflows.</summary>
    ReverseEngineering
}
