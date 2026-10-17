namespace InfiniteLiteToken.Core.Models;

/// <summary>
/// Reports progress updates during dependency bootstrapping.
/// </summary>
public record BootstrapProgress(
    string Component,
    string Status,
    double Percentage,
    bool IsCompleted = false,
    bool IsError = false
);

/// <summary>
/// Status report of installed runtimes and CLI tools.
/// </summary>
public record EnvironmentStatus(
    bool NodeJsAvailable,
    string? NodeJsVersion,
    bool PythonAvailable,
    string? PythonVersion,
    bool LiteLlmInstalled,
    bool UvicornInstalled,
    bool McpInstalled,
    bool ClaudeCodeRouterInstalled,
    bool ClaudeCodeInstalled,
    bool CodexInstalled,
    bool ReaAgentsInstalled
);
