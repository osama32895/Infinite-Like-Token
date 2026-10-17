using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using InfiniteLiteToken.Core.Models;

namespace InfiniteLiteToken.Core.Services;

/// <summary>
/// Generates the LiteLLM proxy configuration YAML file from active keys in KeyVaultService.
/// </summary>
public class LiteLlmConfigBuilder
{
    private readonly KeyVaultService _vaultService;
    private readonly string _configFilePath;

    public LiteLlmConfigBuilder(KeyVaultService vaultService, string? customConfigPath = null)
    {
        _vaultService = vaultService;
        if (string.IsNullOrWhiteSpace(customConfigPath))
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appFolder = Path.Combine(localAppData, "InfiniteLiteToken");
            _configFilePath = Path.Combine(appFolder, "litellm_config.yaml");
        }
        else
        {
            _configFilePath = customConfigPath;
        }
    }

    public string ConfigFilePath => _configFilePath;

    public async Task<string> GenerateConfigYamlAsync(CancellationToken cancellationToken = default)
    {
        var keys = await _vaultService.LoadKeysAsync(cancellationToken);
        var activeKeys = keys.Where(k => k.IsEnabled && !string.IsNullOrWhiteSpace(k.ApiKey)).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("model_list:");

        if (activeKeys.Count == 0)
        {
            sb.AppendLine("  - model_name: tier-pro");
            sb.AppendLine("    litellm_params:");
            sb.AppendLine("      model: gemini/gemini-3.1-pro-preview");
            sb.AppendLine("      api_key: \"placeholder_dummy_key\"");
            sb.AppendLine("  - model_name: tier-flash");
            sb.AppendLine("    litellm_params:");
            sb.AppendLine("      model: gemini/gemini-3.8-flash");
            sb.AppendLine("      api_key: \"placeholder_dummy_key\"");
            sb.AppendLine("  - model_name: tier-lite");
            sb.AppendLine("    litellm_params:");
            sb.AppendLine("      model: gemini/gemini-3.8-flash-lite");
            sb.AppendLine("      api_key: \"placeholder_dummy_key\"");
            sb.AppendLine("  - model_name: tier-rea");
            sb.AppendLine("    litellm_params:");
            sb.AppendLine("      model: gemini/gemini-3.1-pro-preview");
            sb.AppendLine("      api_key: \"placeholder_dummy_key\"");
        }
        else
        {
            foreach (var key in activeKeys)
            {
                AppendDeploymentsForKey(sb, key);
            }
        }

        sb.AppendLine();
        sb.AppendLine("router_settings:");
        sb.AppendLine("  routing_strategy: \"simple-shuffle\"");
        sb.AppendLine("  num_retries: 2");
        sb.AppendLine("  cooldown_time: 60");
        sb.AppendLine("  allowed_fails: 2");
        sb.AppendLine("  fallbacks:");
        sb.AppendLine("    - tier-pro: [\"tier-flash\"]");
        sb.AppendLine("    - tier-flash: [\"tier-lite\"]");
        sb.AppendLine("    - tier-rea: [\"tier-pro\", \"tier-flash\"]");

        sb.AppendLine();
        sb.AppendLine("litellm_settings:");
        sb.AppendLine("  drop_params: true");
        sb.AppendLine("  set_verbose: true");
        sb.AppendLine("  callbacks: [\"engine.router_middleware.router_classifier\"]");

        string yaml = sb.ToString();
        string? dir = Path.GetDirectoryName(_configFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllTextAsync(_configFilePath, yaml, cancellationToken);
        return _configFilePath;
    }

    private static void AppendDeploymentsForKey(StringBuilder sb, ApiKeyEntry key)
    {
        string plainKey = key.ApiKey.Replace("\"", "\\\"");
        string safeNickname = string.IsNullOrWhiteSpace(key.Nickname) ? key.Id.ToString() : key.Nickname.Replace("\"", "'");

        switch (key.Provider)
        {
            case ProviderType.GoogleGemini:
                AppendEntry(sb, "tier-pro", "gemini/gemini-3.1-pro-preview", plainKey, safeNickname);
                AppendEntry(sb, "tier-flash", "gemini/gemini-3.8-flash", plainKey, safeNickname);
                AppendEntry(sb, "tier-lite", "gemini/gemini-3.8-flash-lite", plainKey, safeNickname);
                AppendEntry(sb, "tier-rea", "gemini/gemini-3.1-pro-preview", plainKey, safeNickname);
                break;

            case ProviderType.Groq:
                AppendEntry(sb, "tier-pro", "groq/deepseek-r1-distill-llama-70b", plainKey, safeNickname);
                AppendEntry(sb, "tier-flash", "groq/llama-3.3-70b-versatile", plainKey, safeNickname);
                AppendEntry(sb, "tier-lite", "groq/llama-3.1-8b-instant", plainKey, safeNickname);
                AppendEntry(sb, "tier-rea", "groq/llama-3.3-70b-versatile", plainKey, safeNickname);
                break;

            case ProviderType.NvidiaNim:
                AppendEntry(sb, "tier-pro", "openai/deepseek-ai/deepseek-r1", plainKey, safeNickname, "https://integrate.api.nvidia.com/v1");
                AppendEntry(sb, "tier-flash", "openai/meta/llama-3.3-70b-instruct", plainKey, safeNickname, "https://integrate.api.nvidia.com/v1");
                AppendEntry(sb, "tier-lite", "openai/meta/llama-3.1-8b-instruct", plainKey, safeNickname, "https://integrate.api.nvidia.com/v1");
                AppendEntry(sb, "tier-rea", "openai/meta/llama-3.3-70b-instruct", plainKey, safeNickname, "https://integrate.api.nvidia.com/v1");
                break;

            case ProviderType.OpenRouter:
                AppendEntry(sb, "tier-pro", "openrouter/deepseek/deepseek-r1:free", plainKey, safeNickname);
                AppendEntry(sb, "tier-flash", "openrouter/meta-llama/llama-3.3-70b-instruct:free", plainKey, safeNickname);
                AppendEntry(sb, "tier-lite", "openrouter/meta-llama/llama-3.2-3b-instruct:free", plainKey, safeNickname);
                AppendEntry(sb, "tier-rea", "openrouter/qwen/qwen-2.5-coder-32b-instruct:free", plainKey, safeNickname);
                break;

            case ProviderType.Cerebras:
                AppendEntry(sb, "tier-pro", "cerebras/llama-3.3-70b", plainKey, safeNickname);
                AppendEntry(sb, "tier-flash", "cerebras/llama-3.3-70b", plainKey, safeNickname);
                AppendEntry(sb, "tier-lite", "cerebras/llama3.1-8b", plainKey, safeNickname);
                AppendEntry(sb, "tier-rea", "cerebras/llama-3.3-70b", plainKey, safeNickname);
                break;

            case ProviderType.GitHubModels:
                AppendEntry(sb, "tier-pro", "openai/DeepSeek-R1", plainKey, safeNickname, "https://models.inference.ai.azure.com");
                AppendEntry(sb, "tier-flash", "openai/gpt-4o-mini", plainKey, safeNickname, "https://models.inference.ai.azure.com");
                AppendEntry(sb, "tier-lite", "openai/gpt-4o-mini", plainKey, safeNickname, "https://models.inference.ai.azure.com");
                AppendEntry(sb, "tier-rea", "openai/gpt-4o", plainKey, safeNickname, "https://models.inference.ai.azure.com");
                break;

            case ProviderType.CustomOpenAI:
                AppendEntry(sb, "tier-pro", "openai/gpt-4o", plainKey, safeNickname);
                AppendEntry(sb, "tier-flash", "openai/gpt-4o-mini", plainKey, safeNickname);
                AppendEntry(sb, "tier-lite", "openai/gpt-4o-mini", plainKey, safeNickname);
                AppendEntry(sb, "tier-rea", "openai/gpt-4o", plainKey, safeNickname);
                break;
        }
    }

    private static void AppendEntry(StringBuilder sb, string tierName, string modelTarget, string apiKey, string nickname, string? apiBase = null)
    {
        sb.AppendLine($"  - model_name: {tierName}");
        sb.AppendLine("    litellm_params:");
        sb.AppendLine($"      model: {modelTarget}");
        sb.AppendLine($"      api_key: \"{apiKey}\"");
        if (!string.IsNullOrEmpty(apiBase))
        {
            sb.AppendLine($"      api_base: \"{apiBase}\"");
        }
        sb.AppendLine("      metadata:");
        sb.AppendLine($"        key_nickname: \"{nickname}\"");
    }
}
