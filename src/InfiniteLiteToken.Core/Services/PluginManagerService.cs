using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using InfiniteLiteToken.Core.Models;

namespace InfiniteLiteToken.Core.Services;

public class PluginManagerService
{
    private readonly string _pluginsDirectory;
    private readonly string _configFile;
    private readonly List<PluginDefinition> _activePlugins = new();

    public event Action<PluginDefinition>? PluginStateChanged;

    public PluginManagerService(string? customPluginsDir = null)
    {
        if (string.IsNullOrWhiteSpace(customPluginsDir))
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _pluginsDirectory = Path.Combine(localAppData, "InfiniteLiteToken", "plugins");
        }
        else
        {
            _pluginsDirectory = customPluginsDir;
        }

        _configFile = Path.Combine(_pluginsDirectory, "plugins_config.json");
        Directory.CreateDirectory(_pluginsDirectory);
        _ = LoadPluginsAsync();
    }

    public IReadOnlyList<PluginDefinition> Plugins => _activePlugins.AsReadOnly();

    public async Task LoadPluginsAsync(CancellationToken cancellationToken = default)
    {
        _activePlugins.Clear();
        var catalog = GetCuratedCatalog();

        if (File.Exists(_configFile))
        {
            try
            {
                string json = await File.ReadAllTextAsync(_configFile, cancellationToken);
                var saved = JsonSerializer.Deserialize<List<PluginDefinition>>(json);
                if (saved != null)
                {
                    foreach (var p in saved)
                    {
                        var existing = catalog.FirstOrDefault(c => c.Id == p.Id);
                        if (existing != null)
                        {
                            existing.IsEnabled = p.IsEnabled;
                            existing.IsInstalled = p.IsInstalled;
                        }
                        else
                        {
                            catalog.Add(p);
                        }
                    }
                }
            }
            catch
            {
            }
        }

        _activePlugins.AddRange(catalog);
    }

    public async Task SavePluginsConfigAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            string json = JsonSerializer.Serialize(_activePlugins, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_configFile, json, cancellationToken);
        }
        catch
        {
        }
    }

    public async Task TogglePluginAsync(string pluginId, bool enable, CancellationToken cancellationToken = default)
    {
        var plugin = _activePlugins.FirstOrDefault(p => p.Id == pluginId);
        if (plugin != null)
        {
            plugin.IsEnabled = enable;
            await SavePluginsConfigAsync(cancellationToken);
            PluginStateChanged?.Invoke(plugin);
        }
    }

    public async Task InstallCatalogPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        var plugin = _activePlugins.FirstOrDefault(p => p.Id == pluginId);
        if (plugin != null)
        {
            plugin.IsInstalled = true;
            plugin.IsEnabled = true;
            await SavePluginsConfigAsync(cancellationToken);
            PluginStateChanged?.Invoke(plugin);
        }
    }

    public async Task ImportCustomPluginFolderAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(folderPath)) return;

        string folderName = Path.GetFileName(folderPath);
        var plugin = new PluginDefinition
        {
            Id = $"custom_{Guid.NewGuid():N}",
            Name = folderName,
            Description = $"Custom plugin imported from {folderPath}",
            Type = PluginType.CodexSkillPlugin,
            Source = PluginSource.LocalFolder,
            IsInstalled = true,
            IsEnabled = true,
            InstallPath = folderPath
        };

        // Parse SKILL.md or commands if available
        string skillFile = Path.Combine(folderPath, "SKILL.md");
        if (File.Exists(skillFile))
        {
            plugin.SystemPromptExtension = await File.ReadAllTextAsync(skillFile, cancellationToken);
        }

        _activePlugins.Add(plugin);
        await SavePluginsConfigAsync(cancellationToken);
        PluginStateChanged?.Invoke(plugin);
    }

    public string BuildAggregatedSystemInstructions()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== ACTIVE EXTENSIONS & PLUGINS INSTRUCTIONS ===");

        foreach (var plugin in _activePlugins.Where(p => p.IsEnabled && p.IsInstalled))
        {
            if (!string.IsNullOrWhiteSpace(plugin.SystemPromptExtension))
            {
                sb.AppendLine($"\n[Plugin: {plugin.Name}] ({plugin.Type})");
                sb.AppendLine(plugin.SystemPromptExtension.Trim());
            }

            if (plugin.SlashCommands.Count > 0)
            {
                sb.AppendLine($"Available Commands: {string.Join(", ", plugin.SlashCommands)}");
            }
        }

        sb.AppendLine("================================================");
        return sb.ToString();
    }

    public static List<PluginDefinition> GetCuratedCatalog()
    {
        return new List<PluginDefinition>
        {
            new()
            {
                Id = "claude-arch-pack",
                Name = "Claude Code Architect Pack",
                Description = "Provides multi-agent system design, Clean Architecture, and Domain-Driven Design patterns.",
                Type = PluginType.ClaudeCodePlugin,
                IsInstalled = true,
                IsEnabled = true,
                SlashCommands = new() { "/arch", "/review", "/refactor" },
                SystemPromptExtension = "You are an elite Software Architect. Prioritize SOLID principles, modular decomposition, and explicit dependency management."
            },
            new()
            {
                Id = "codex-fullstack",
                Name = "Codex Full-Stack Supercharger",
                Description = "High-velocity web development instructions for React, Next.js, ASP.NET Core, and Tailwind CSS.",
                Type = PluginType.CodexSkillPlugin,
                IsInstalled = true,
                IsEnabled = true,
                SlashCommands = new() { "/component", "/api", "/crud" },
                SystemPromptExtension = "Expert in modern full-stack web engineering. Generate concise, production-ready TypeScript, C#, and SQL."
            },
            new()
            {
                Id = "rea-ghidra-bridge",
                Name = "REA Ghidra & ILSpy Deep Bridge",
                Description = "Reverse engineering prompt pack and decompilation tool caller for .NET assemblies and native PEs.",
                Type = PluginType.McpServerPlugin,
                IsInstalled = true,
                IsEnabled = true,
                McpExecutable = "rea-agents",
                SlashCommands = new() { "/decompile", "/symbols", "/entropy" },
                SystemPromptExtension = "You are a world-class reverse engineer. Analyze disassembly, PE headers, CIL bytecode, and reconstruct pristine high-level source code."
            },
            new()
            {
                Id = "mcp-git-automator",
                Name = "MCP Git & GitHub Automator",
                Description = "Automated commit generation, smart PR review, merge conflict resolver, and branch management.",
                Type = PluginType.McpServerPlugin,
                IsInstalled = true,
                IsEnabled = true,
                McpExecutable = "npx",
                McpArguments = "-y @modelcontextprotocol/server-git",
                SlashCommands = new() { "/commit", "/pr", "/diff" }
            },
            new()
            {
                Id = "unity-csharp-dev",
                Name = "Unity & C# Game Dev Assistant",
                Description = "Game design patterns, ECS/DOTS, shader guidance, and memory optimization for Unity games.",
                Type = PluginType.CodexSkillPlugin,
                IsInstalled = false,
                IsEnabled = false,
                SlashCommands = new() { "/gameplay", "/shader", "/opt" },
                SystemPromptExtension = "Specialize in Unity Game Development with C#. Avoid GC allocation in hot loops, optimize draw calls, and use clean component architectures."
            },
            new()
            {
                Id = "flutter-firebase",
                Name = "Flutter & Firebase Architect",
                Description = "Cross-platform mobile state management (Bloc/Riverpod) and Firebase security rules.",
                Type = PluginType.ClaudeCodePlugin,
                IsInstalled = false,
                IsEnabled = false,
                SlashCommands = new() { "/widget", "/bloc", "/rules" }
            },
            new()
            {
                Id = "security-auditor",
                Name = "Security & Penetration Auditor",
                Description = "OWASP Top 10 scanner, cryptographic vulnerability analyzer, and safe sanitization checks.",
                Type = PluginType.CodexSkillPlugin,
                IsInstalled = true,
                IsEnabled = true,
                SlashCommands = new() { "/audit", "/vuln", "/sanitize" },
                SystemPromptExtension = "Perform strict security audits. Flag injection vectors, insecure deserialization, broken authentication, and plaintext secrets."
            },
            new()
            {
                Id = "db-sql-optimizer",
                Name = "Database Schema & SQL Optimizer",
                Description = "Index strategy, EXPLAIN ANALYZE interpretation, EF Core migrations, and query tuning.",
                Type = PluginType.ClaudeCodePlugin,
                IsInstalled = false,
                IsEnabled = false,
                SlashCommands = new() { "/query", "/index", "/schema" }
            },
            new()
            {
                Id = "web-docs-scraper",
                Name = "Web Scraper & Docs Reader",
                Description = "Headless browser documentation extraction and offline markdown synthesis.",
                Type = PluginType.McpServerPlugin,
                IsInstalled = false,
                IsEnabled = false,
                McpExecutable = "npx",
                McpArguments = "-y @modelcontextprotocol/server-puppeteer"
            },
            new()
            {
                Id = "unit-test-gen",
                Name = "Automated Unit Test Generator",
                Description = "Generates high-coverage xUnit, NUnit, PyTest, or Jest test suites with mock fixtures.",
                Type = PluginType.ClaudeCodePlugin,
                IsInstalled = true,
                IsEnabled = true,
                SlashCommands = new() { "/test", "/mock", "/coverage" },
                SystemPromptExtension = "Generate thorough automated tests covering edge cases, null arguments, exceptions, and boundary limits."
            },
            new()
            {
                Id = "mcp-filesystem",
                Name = "MCP Safe Filesystem Access",
                Description = "Sandboxed workspace file manipulation and structured tree inspection via MCP.",
                Type = PluginType.McpServerPlugin,
                IsInstalled = true,
                IsEnabled = true,
                McpExecutable = "npx",
                McpArguments = "-y @modelcontextprotocol/server-filesystem"
            },
            new()
            {
                Id = "seq-thinking",
                Name = "Sequential Thinking Reasoning Engine",
                Description = "Enables multi-step step-by-step cognitive decomposition for hard logic and math puzzles.",
                Type = PluginType.McpServerPlugin,
                IsInstalled = true,
                IsEnabled = true,
                McpExecutable = "npx",
                McpArguments = "-y @modelcontextprotocol/server-sequential-thinking"
            }
        };
    }
}
