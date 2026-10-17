using System;
using System.Collections.Generic;

namespace InfiniteLikeToken.Core.Models;

public enum PluginType
{
    ClaudeCodePlugin,
    CodexSkillPlugin,
    McpServerPlugin
}

public enum PluginSource
{
    CuratedCatalog,
    LocalFolder,
    GitHubRepo,
    CustomFile
}

public class PluginDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Author { get; set; } = "Community";
    public PluginType Type { get; set; } = PluginType.ClaudeCodePlugin;
    public PluginSource Source { get; set; } = PluginSource.CuratedCatalog;
    public bool IsEnabled { get; set; } = true;
    public bool IsInstalled { get; set; }
    public string InstallPath { get; set; } = string.Empty;
    public string RepositoryUrl { get; set; } = string.Empty;
    public string SystemPromptExtension { get; set; } = string.Empty;
    public List<string> SlashCommands { get; set; } = new();
    public List<string> ExposedTools { get; set; } = new();

    // For MCP Stdio Servers
    public string? McpExecutable { get; set; }
    public string? McpArguments { get; set; }
    public Dictionary<string, string> McpEnvironmentVariables { get; set; } = new();
}

public record ReaTargetArtifact(
    string Path,
    string FileName,
    string Extension,
    long FileSize,
    bool IsDirectory,
    double Entropy,
    IReadOnlyList<string> DiscoveredSections,
    IReadOnlyList<string> ExtractedImports,
    IReadOnlyList<string> ExtractedExports,
    IReadOnlyList<string> ExtractedStrings,
    string DecompiledPreview
);
