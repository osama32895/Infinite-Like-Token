using System;
using System.Collections.Generic;

namespace InfiniteLikeToken.Core.Models;

public enum AuthStyle
{
    BearerToken,
    QueryParamKey,
    XApiKey,
    None
}

public record ProviderMetadata(
    ProviderType Provider,
    string DisplayName,
    string DefaultBaseUrl,
    AuthStyle AuthHeaderStyle,
    long FreeTierDailyTokenLimit,
    int FreeTierDailyRequestLimit,
    IReadOnlyList<string> RecommendedProModels,
    IReadOnlyList<string> RecommendedFlashModels,
    IReadOnlyList<string> RecommendedLiteModels,
    IReadOnlyList<string> RecommendedReaModels,
    string GetApiKeyUrl
);

public static class ProviderCatalog
{
    private static readonly Dictionary<ProviderType, ProviderMetadata> Catalog = new()
    {
        [ProviderType.GoogleGemini] = new(
              ProviderType.GoogleGemini,
              "Google AI Studio (Gemini)",
              "https://generativelanguage.googleapis.com/v1beta",
              AuthStyle.QueryParamKey,
              1_000_000,
              1500,
              new[] { "gemini-3.1-pro-preview" },
              new[] { "gemini-3.7-flash", "gemini-3.6-flash", "gemini-2.5-flash" },
              new[] { "gemini-3.5-flash-lite" },
              new[] { "gemini-3.1-pro-preview" },
              "https://aistudio.google.com/app/apikey"
          ),
        [ProviderType.Groq] = new(
            ProviderType.Groq,
            "Groq Cloud",
            "https://api.groq.com/openai/v1",
            AuthStyle.BearerToken,
            500_000,
            14400,
            new[] { "deepseek-r1-distill-llama-70b", "llama-3.3-70b-versatile" },
            new[] { "llama-3.3-70b-versatile", "mixtral-8x7b-32768" },
            new[] { "llama-3.1-8b-instant", "gemma2-9b-it" },
            new[] { "llama-3.3-70b-versatile", "qwen-2.5-coder-32b" },
            "https://console.groq.com/keys"
        ),
        [ProviderType.NvidiaNim] = new(
            ProviderType.NvidiaNim,
            "NVIDIA NIM",
            "https://integrate.api.nvidia.com/v1",
            AuthStyle.BearerToken,
            300_000,
            1000,
            new[] { "deepseek-ai/deepseek-r1", "meta/llama-3.3-70b-instruct" },
            new[] { "meta/llama-3.3-70b-instruct", "mistralai/mistral-large-2-instruct" },
            new[] { "meta/llama-3.1-8b-instruct", "google/gemma-2-9b-it" },
            new[] { "meta/llama-3.3-70b-instruct", "qwen/qwen2.5-coder-32b-instruct" },
            "https://build.nvidia.com/"
        ),
        [ProviderType.OpenRouter] = new(
            ProviderType.OpenRouter,
            "OpenRouter Free Pool",
            "https://openrouter.ai/api/v1",
            AuthStyle.BearerToken,
            400_000,
            1000,
            new[] { "deepseek/deepseek-r1:free", "google/gemini-2.0-flash-exp:free" },
            new[] { "meta-llama/llama-3.3-70b-instruct:free", "mistralai/mistral-7b-instruct:free" },
            new[] { "meta-llama/llama-3.2-3b-instruct:free", "google/gemini-flash-1.5:free" },
            new[] { "qwen/qwen-2.5-coder-32b-instruct:free", "deepseek/deepseek-r1:free" },
            "https://openrouter.ai/keys"
        ),
        [ProviderType.Cerebras] = new(
            ProviderType.Cerebras,
            "Cerebras Ultra-Inference",
            "https://api.cerebras.ai/v1",
            AuthStyle.BearerToken,
            1_000_000,
            14400,
            new[] { "llama-3.3-70b", "llama3.1-70b" },
            new[] { "llama-3.3-70b", "llama3.1-8b" },
            new[] { "llama3.1-8b" },
            new[] { "llama-3.3-70b" },
            "https://cloud.cerebras.ai/"
        ),
        [ProviderType.GitHubModels] = new(
            ProviderType.GitHubModels,
            "GitHub Models (Azure AI)",
            "https://models.inference.ai.azure.com",
            AuthStyle.BearerToken,
            200_000,
            500,
            new[] { "DeepSeek-R1", "gpt-4o" },
            new[] { "gpt-4o-mini", "meta-llama-3.1-70b-instruct" },
            new[] { "gpt-4o-mini", "meta-llama-3.1-8b-instruct" },
            new[] { "gpt-4o", "DeepSeek-R1" },
            "https://github.com/marketplace/models"
        ),
        [ProviderType.GitHubCopilot] = new(
            ProviderType.GitHubCopilot,
            "GitHub Copilot API",
            "https://api.githubcopilot.com",
            AuthStyle.BearerToken,
            500_000,
            2000,
            new[] { "claude-3.5-sonnet", "gpt-4o" },
            new[] { "gpt-4o-mini", "claude-3.5-haiku" },
            new[] { "gpt-4o-mini" },
            new[] { "claude-3.5-sonnet", "gpt-4o" },
            "https://github.com/settings/copilot"
        ),
        [ProviderType.DeepSeek] = new(
            ProviderType.DeepSeek,
            "DeepSeek Official",
            "https://api.deepseek.com/v1",
            AuthStyle.BearerToken,
            10_000_000,
            50000,
            new[] { "deepseek-reasoner", "deepseek-chat" },
            new[] { "deepseek-chat" },
            new[] { "deepseek-chat" },
            new[] { "deepseek-reasoner", "deepseek-chat" },
            "https://platform.deepseek.com/api_keys"
        ),
        [ProviderType.CloudflareWorkersAI] = new(
            ProviderType.CloudflareWorkersAI,
            "Cloudflare Workers AI",
            "https://api.cloudflare.com/client/v4/accounts/{account_id}/ai/v1",
            AuthStyle.BearerToken,
            300_000,
            10000,
            new[] { "@cf/deepseek-ai/deepseek-r1-distill-qwen-32b", "@cf/meta/llama-3.3-70b-instruct" },
            new[] { "@cf/meta/llama-3.1-70b-instruct", "@cf/mistral/mistral-7b-instruct-v0.2" },
            new[] { "@cf/meta/llama-3.1-8b-instruct", "@cf/qwen/qwen1.5-7b-chat" },
            new[] { "@cf/qwen/qwen2.5-coder-7b-instruct", "@cf/meta/llama-3.3-70b-instruct" },
            "https://dash.cloudflare.com/"
        ),
        [ProviderType.AtlasCloud] = new(
            ProviderType.AtlasCloud,
            "Atlas Cloud",
            "https://api.atlascloud.ai/v1",
            AuthStyle.BearerToken,
            500_000,
            1000,
            new[] { "deepseek-r1", "llama-3.3-70b" },
            new[] { "llama-3.3-70b" },
            new[] { "llama-3.1-8b" },
            new[] { "deepseek-r1", "qwen-2.5-coder-32b" },
            "https://atlascloud.ai"
        ),
        [ProviderType.AnthropicClaude] = new(
            ProviderType.AnthropicClaude,
            "Anthropic Claude",
            "https://api.anthropic.com/v1",
            AuthStyle.XApiKey,
            1_000_000,
            5000,
            new[] { "claude-3-7-sonnet-20250219", "claude-3-5-sonnet-20241022" },
            new[] { "claude-3-5-haiku-20241022" },
            new[] { "claude-3-5-haiku-20241022" },
            new[] { "claude-3-7-sonnet-20250219", "claude-3-5-sonnet-20241022" },
            "https://console.anthropic.com/settings/keys"
        ),
        [ProviderType.OpenAIGpt] = new(
            ProviderType.OpenAIGpt,
            "OpenAI Official",
            "https://api.openai.com/v1",
            AuthStyle.BearerToken,
            1_000_000,
            10000,
            new[] { "o3-mini", "o1", "gpt-4o" },
            new[] { "gpt-4o", "gpt-4o-mini" },
            new[] { "gpt-4o-mini" },
            new[] { "gpt-4o", "o3-mini" },
            "https://platform.openai.com/api-keys"
        ),
        [ProviderType.OllamaLocal] = new(
            ProviderType.OllamaLocal,
            "Ollama (Local Offline)",
            "http://127.0.0.1:11434/v1",
            AuthStyle.None,
            100_000_000,
            1000000,
            new[] { "deepseek-r1:32b", "qwen2.5-coder:32b", "llama3.3:70b" },
            new[] { "llama3.1:8b", "qwen2.5-coder:7b", "mistral:7b" },
            new[] { "llama3.2:3b", "qwen2.5:3b" },
            new[] { "qwen2.5-coder:32b", "deepseek-r1:14b" },
            "https://ollama.com"
        ),
        [ProviderType.LMStudioLocal] = new(
            ProviderType.LMStudioLocal,
            "LM Studio (Local Offline)",
            "http://127.0.0.1:1234/v1",
            AuthStyle.None,
            100_000_000,
            1000000,
            new[] { "local-model" },
            new[] { "local-model" },
            new[] { "local-model" },
            new[] { "local-model" },
            "https://lmstudio.ai"
        ),
        [ProviderType.MistralLaPlateforme] = new(
            ProviderType.MistralLaPlateforme,
            "Mistral AI (La Plateforme)",
            "https://api.mistral.ai/v1",
            AuthStyle.BearerToken,
            1_000_000,
            5000,
            new[] { "mistral-large-latest", "pixtral-large-latest" },
            new[] { "mistral-small-latest", "codestral-latest" },
            new[] { "open-mistral-7b", "open-mixtral-8x7b" },
            new[] { "codestral-latest", "mistral-large-latest" },
            "https://console.mistral.ai/api-keys/"
        ),
        [ProviderType.Codestral] = new(
            ProviderType.Codestral,
            "Mistral Codestral Dedicated",
            "https://codestral.mistral.ai/v1",
            AuthStyle.BearerToken,
            1_000_000,
            5000,
            new[] { "codestral-latest" },
            new[] { "codestral-latest" },
            new[] { "codestral-latest" },
            new[] { "codestral-latest" },
            "https://console.mistral.ai/api-keys/"
        ),
        [ProviderType.TogetherAI] = new(
            ProviderType.TogetherAI,
            "Together AI",
            "https://api.together.xyz/v1",
            AuthStyle.BearerToken,
            500_000,
            2000,
            new[] { "deepseek-ai/DeepSeek-R1", "meta-llama/Llama-3.3-70b-instruct-turbo" },
            new[] { "meta-llama/Meta-Llama-3.1-70B-Instruct-Turbo", "mistralai/Mixtral-8x7B-Instruct-v0.1" },
            new[] { "meta-llama/Meta-Llama-3.1-8B-Instruct-Turbo" },
            new[] { "Qwen/Qwen2.5-Coder-32B-Instruct", "deepseek-ai/DeepSeek-R1" },
            "https://api.together.ai/settings/api-keys"
        ),
        [ProviderType.FireworksAI] = new(
            ProviderType.FireworksAI,
            "Fireworks AI",
            "https://api.fireworks.ai/inference/v1",
            AuthStyle.BearerToken,
            500_000,
            2000,
            new[] { "accounts/fireworks/models/deepseek-r1", "accounts/fireworks/models/llama-v3p3-70b-instruct" },
            new[] { "accounts/fireworks/models/llama-v3p1-70b-instruct" },
            new[] { "accounts/fireworks/models/llama-v3p1-8b-instruct" },
            new[] { "accounts/fireworks/models/qwen2p5-coder-32b-instruct" },
            "https://fireworks.ai/api-keys"
        ),
        [ProviderType.SambaNova] = new(
            ProviderType.SambaNova,
            "SambaNova Fast Cloud",
            "https://api.sambanova.ai/v1",
            AuthStyle.BearerToken,
            1_000_000,
            10000,
            new[] { "DeepSeek-R1", "Meta-Llama-3.3-70B-Instruct" },
            new[] { "Meta-Llama-3.1-70B-Instruct" },
            new[] { "Meta-Llama-3.1-8B-Instruct" },
            new[] { "Qwen2.5-Coder-32B-Instruct", "DeepSeek-R1" },
            "https://cloud.sambanova.ai/apis"
        ),
        [ProviderType.Cohere] = new(
            ProviderType.Cohere,
            "Cohere Command",
            "https://api.cohere.com/v2",
            AuthStyle.BearerToken,
            200_000,
            1000,
            new[] { "command-r-plus-08-2024", "command-r-plus" },
            new[] { "command-r-08-2024", "command-r" },
            new[] { "command-light" },
            new[] { "command-r-plus-08-2024" },
            "https://dashboard.cohere.com/api-keys"
        ),
        [ProviderType.Perplexity] = new(
            ProviderType.Perplexity,
            "Perplexity AI",
            "https://api.perplexity.ai",
            AuthStyle.BearerToken,
            300_000,
            1000,
            new[] { "sonar-reasoning-pro", "sonar-pro" },
            new[] { "sonar" },
            new[] { "sonar" },
            new[] { "sonar-reasoning-pro" },
            "https://www.perplexity.ai/settings/api"
        ),
        [ProviderType.Hyperbolic] = new(
            ProviderType.Hyperbolic,
            "Hyperbolic AI",
            "https://api.hyperbolic.xyz/v1",
            AuthStyle.BearerToken,
            500_000,
            5000,
            new[] { "deepseek-ai/DeepSeek-R1", "meta-llama/Llama-3.3-70B-Instruct" },
            new[] { "meta-llama/Meta-Llama-3.1-70B-Instruct" },
            new[] { "meta-llama/Meta-Llama-3.1-8B-Instruct" },
            new[] { "Qwen/Qwen2.5-Coder-32B-Instruct" },
            "https://app.hyperbolic.xyz/settings"
        ),
        [ProviderType.ChutesAI] = new(
            ProviderType.ChutesAI,
            "Chutes AI",
            "https://chutes.ai/v1",
            AuthStyle.BearerToken,
            500_000,
            2000,
            new[] { "deepseek-ai/DeepSeek-R1", "llama-3.3-70b-instruct" },
            new[] { "llama-3.3-70b-instruct" },
            new[] { "llama-3.1-8b-instruct" },
            new[] { "qwen2.5-coder-32b-instruct" },
            "https://chutes.ai"
        ),
        [ProviderType.NovitaAI] = new(
            ProviderType.NovitaAI,
            "Novita AI",
            "https://api.novita.ai/v3/openai",
            AuthStyle.BearerToken,
            500_000,
            2000,
            new[] { "deepseek/deepseek-r1", "meta-llama/llama-3.3-70b-instruct" },
            new[] { "meta-llama/llama-3.1-70b-instruct" },
            new[] { "meta-llama/llama-3.1-8b-instruct" },
            new[] { "qwen/qwen-2.5-coder-32b-instruct" },
            "https://novita.ai/settings/key-management"
        ),
        [ProviderType.SiliconFlow] = new(
            ProviderType.SiliconFlow,
            "SiliconFlow",
            "https://api.siliconflow.cn/v1",
            AuthStyle.BearerToken,
            1_000_000,
            10000,
            new[] { "deepseek-ai/DeepSeek-R1", "deepseek-ai/DeepSeek-V3" },
            new[] { "meta-llama/Meta-Llama-3.3-70B-Instruct", "Qwen/Qwen2.5-72B-Instruct" },
            new[] { "meta-llama/Meta-Llama-3.1-8B-Instruct", "Qwen/Qwen2.5-7B-Instruct" },
            new[] { "Qwen/Qwen2.5-Coder-32B-Instruct", "deepseek-ai/DeepSeek-R1" },
            "https://cloud.siliconflow.cn/account/ak"
        ),
        [ProviderType.HuggingFaceInference] = new(
            ProviderType.HuggingFaceInference,
            "Hugging Face Inference API",
            "https://api-inference.huggingface.co/v1",
            AuthStyle.BearerToken,
            300_000,
            2000,
            new[] { "deepseek-ai/DeepSeek-R1", "meta-llama/Llama-3.3-70B-Instruct" },
            new[] { "meta-llama/Meta-Llama-3.1-70B-Instruct" },
            new[] { "meta-llama/Meta-Llama-3.1-8B-Instruct" },
            new[] { "Qwen/Qwen2.5-Coder-32B-Instruct" },
            "https://huggingface.co/settings/tokens"
        ),
        [ProviderType.CustomOpenAI] = new(
            ProviderType.CustomOpenAI,
            "Custom OpenAI-Compatible API",
            "https://api.openai.com/v1",
            AuthStyle.BearerToken,
            1_000_000,
            10000,
            new[] { "gpt-4o", "claude-3-5-sonnet" },
            new[] { "gpt-4o-mini", "llama-3.3-70b" },
            new[] { "gpt-4o-mini", "llama-3.1-8b" },
            new[] { "gpt-4o", "qwen-coder" },
            "https://platform.openai.com/api-keys"
        )
    };

    public static ProviderMetadata GetMetadata(ProviderType provider)
    {
        if (Catalog.TryGetValue(provider, out var meta))
        {
            return meta;
        }

        return Catalog[ProviderType.CustomOpenAI];
    }

    public static IReadOnlyCollection<ProviderMetadata> GetAll() => Catalog.Values;
}
