using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using InfiniteLikeToken.Core.Models;

namespace InfiniteLikeToken.Core.Services;

/// <summary>
/// Built-in Coding Agent with SSE streaming, tool execution, and native C# smart multi-key fallback router.
/// </summary>
public class BuiltInAgentService
{
    private readonly HttpClient _httpClient;
    private readonly ProxyProcessManager _proxyManager;
    private readonly KeyVaultService _vaultService;
    private readonly TelemetryService _telemetryService;
    private readonly RuntimeBootstrapper _bootstrapper;

    public BuiltInAgentService(
        HttpClient httpClient,
        ProxyProcessManager proxyManager,
        KeyVaultService vaultService,
        TelemetryService telemetryService,
        RuntimeBootstrapper bootstrapper)
    {
        _httpClient = httpClient;
        _proxyManager = proxyManager;
        _vaultService = vaultService;
        _telemetryService = telemetryService;
        _bootstrapper = bootstrapper;
    }

    /// <summary>
    /// Streams chat completions using either the local LiteLLM proxy (if running) or native C# multi-key smart router.
    /// </summary>
    public async IAsyncEnumerable<string> StreamChatAsync(
        string prompt,
        string? workspaceFolder = null,
        string model = "infinite-auto",
        List<AgentMessage>? history = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Determine target model tier and resolve via Native C# Multi-Key Smart Router if proxy is not running
        if (!_proxyManager.IsRunning)
        {
            await foreach (var token in StreamNativeSmartRouterAsync(prompt, model, history, cancellationToken))
            {
                yield return token;
            }
            yield break;
        }

        // Otherwise stream through local proxy
        var messages = new List<object>();
        if (history != null)
        {
            foreach (var h in history)
            {
                messages.Add(new { role = h.Role, content = h.Content });
            }
        }
        messages.Add(new { role = "user", content = prompt });

        var payload = new { model, messages, stream = true };
        string jsonBody = JsonSerializer.Serialize(payload);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_proxyManager.ProxyBaseUrl}/v1/chat/completions");
        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (line.StartsWith("data: "))
            {
                string data = line[6..].Trim();
                if (data == "[DONE]") break;

                string? deltaText = ExtractDeltaContent(data);
                if (!string.IsNullOrEmpty(deltaText))
                {
                    yield return deltaText;
                }
            }
        }
    }

    private async IAsyncEnumerable<string> StreamNativeSmartRouterAsync(
        string prompt,
        string virtualModel,
        List<AgentMessage>? history,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var keys = await _vaultService.LoadKeysAsync(cancellationToken);
        var activeKeys = keys.FindAll(k => k.IsEnabled && !string.IsNullOrWhiteSpace(k.ApiKey) && k.HealthStatus != KeyHealthStatus.QuotaExhausted);

        if (activeKeys.Count == 0)
        {
            yield return "[InfiniteLikeToken] Error: No active or healthy API keys found in vault. Please add free keys in the 'Infinite Key Pool' tab.";
            yield break;
        }

        ModelTier tier = ClassifyPromptComplexity(prompt, virtualModel);
        ApiKeyEntry? selectedKey = null;
        string targetModelName = string.Empty;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        bool success = false;
        string fullAccumulated = "";
        System.Net.Http.HttpResponseMessage? activeResponse = null;
        bool isGemini = false;

        foreach (var key in activeKeys)
        {
            selectedKey = key;
            var meta = ProviderCatalog.GetMetadata(key.Provider);
            bool isExactModel = virtualModel != "infinite-auto" && !virtualModel.StartsWith("tier-");
            
            var modelsToTry = new List<string>();
            if (isExactModel) {
                modelsToTry.Add(virtualModel);
            } else {
                var tierModels = tier switch
                {
                    ModelTier.DeepReasoningPro => meta.RecommendedProModels,
                    ModelTier.StandardFlash => meta.RecommendedFlashModels,
                    ModelTier.UltraFastLite => meta.RecommendedLiteModels,
                    ModelTier.ReverseEngineering => meta.RecommendedReaModels,
                    _ => meta.RecommendedFlashModels
                };
                if (tierModels != null) modelsToTry.AddRange(tierModels);
            }

            isGemini = key.Provider == ProviderType.GoogleGemini;

            foreach (var modelVariant in modelsToTry)
            {
                targetModelName = modelVariant;
                string url = isGemini
                    ? $"{meta.DefaultBaseUrl}/models/{targetModelName}:streamGenerateContent?alt=sse&key={Uri.EscapeDataString(key.ApiKey)}"
                    : $"{meta.DefaultBaseUrl}/chat/completions";

                object body;
                if (isGemini)
                {
                    var geminiContents = new List<object>();
                    object? sysInst = null;
                    
                    if (history != null)
                    {
                        foreach (var h in history)
                        {
                            if (h.Role == "system") {
                                sysInst = new { parts = new[] { new { text = h.Content } } };
                                continue;
                            }
                            string gRole = h.Role == "assistant" ? "model" : "user";
                            geminiContents.Add(new { role = gRole, parts = new[] { new { text = h.Content } } });
                        }
                    }
                    geminiContents.Add(new { role = "user", parts = new[] { new { text = prompt } } });
                    
                    if (sysInst != null) {
                        body = new { system_instruction = sysInst, contents = geminiContents };
                    } else {
                        body = new { contents = geminiContents };
                    }
                }
                else
                {
                    var oaiMessages = new List<object>();
                    if (history != null)
                    {
                        foreach (var h in history) oaiMessages.Add(new { role = h.Role, content = h.Content });
                    }
                    oaiMessages.Add(new { role = "user", content = prompt });
                    body = new { model = targetModelName, messages = oaiMessages, stream = true };
                }

                int maxRetries = 3;
                for (int attempt = 0; attempt < maxRetries; attempt++)
                {
                    var httpRequest = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, url);
                    httpRequest.Content = new System.Net.Http.StringContent(System.Text.Json.JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");

                    if (!isGemini && meta.AuthHeaderStyle == AuthStyle.BearerToken)
                    {
                        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key.ApiKey);
                    }
                    else if (!isGemini && meta.AuthHeaderStyle == AuthStyle.XApiKey)
                    {
                        httpRequest.Headers.Add("x-api-key", key.ApiKey);
                        httpRequest.Headers.Add("anthropic-version", "2023-06-01");
                    }

                    try
                    {
                        var resp = await _httpClient.SendAsync(httpRequest, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                        if (resp.IsSuccessStatusCode)
                        {
                            activeResponse = resp;
                            success = true;
                            break;
                        }
                        else if ((int)resp.StatusCode == 429 || (int)resp.StatusCode >= 500)
                        {
                            if (attempt == maxRetries - 1) break;
                            await Task.Delay(1500, cancellationToken);
                        }
                        else
                        {
                            activeResponse = resp;
                            break;
                        }
                    }
                    catch
                    {
                        if (attempt == maxRetries - 1) break;
                        await Task.Delay(1500, cancellationToken);
                    }
                }
                
                if (success) break;
            }
            
            if (success) {
                break;
            } else {
                key.HealthStatus = KeyHealthStatus.Cooldown;
                key.LastRateLimitedAt = DateTime.Now;
            }
        }

        if (activeResponse != null)
        {
            using (activeResponse)
            {
                if (activeResponse.IsSuccessStatusCode)
                {
                    using var stream = await activeResponse.Content.ReadAsStreamAsync(cancellationToken);
                    using var reader = new System.IO.StreamReader(stream);

                    while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
                    {
                        string? line = await reader.ReadLineAsync(cancellationToken);
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        string? chunk = ExtractTokenFromStreamLine(line, isGemini);
                        if (!string.IsNullOrEmpty(chunk))
                        {
                            fullAccumulated += chunk;
                            yield return chunk;
                        }
                    }
                }
                else
                {
                    string err = await activeResponse.Content.ReadAsStringAsync(cancellationToken);
                    yield return $"[HTTP Error {(int)activeResponse.StatusCode}]: {err}";
                }
            }
        }
        else
        {
            yield return "[InfiniteLikeToken] Error: All active keys exhausted or in cooldown.";
        }

        sw.Stop();
        if (selectedKey != null)
        {
            long promptTokens = prompt.Length / 4;
            long compTokens = fullAccumulated.Length / 4;
            await _telemetryService.RecordUsageAsync(selectedKey, targetModelName, promptTokens, compTokens, sw.ElapsedMilliseconds, success);
            await _vaultService.SaveKeysAsync(keys, cancellationToken);
        }
    }

    private static ModelTier ClassifyPromptComplexity(string prompt, string modelOverride)
    {
        if (modelOverride == "tier-pro") return ModelTier.DeepReasoningPro;
        if (modelOverride == "tier-lite") return ModelTier.UltraFastLite;
        if (modelOverride == "tier-rea") return ModelTier.ReverseEngineering;

        string lower = prompt.ToLowerInvariant();
        if (lower.Contains("ghidra") || lower.Contains("ilspy") || lower.Contains("decompile") || lower.Contains("disassemble") || lower.Contains("apk"))
            return ModelTier.ReverseEngineering;
        if (lower.Contains("architecture") || lower.Contains("refactor") || lower.Contains("memory leak") || lower.Contains("deadlock") || prompt.Length > 4000)
            return ModelTier.DeepReasoningPro;
        if (prompt.Length < 300 && (lower.Contains("typo") || lower.Contains("fix") || lower.Contains("comment")))
            return ModelTier.UltraFastLite;

        return ModelTier.StandardFlash;
    }

    private static string? ExtractTokenFromStreamLine(string line, bool isGemini)
    {
        if (line.StartsWith("data: ")) line = line[6..].Trim();
        if (line == "[DONE]") return null;
        try
        {
            using var doc = JsonDocument.Parse(line);
            if (isGemini)
            {
                if (doc.RootElement.TryGetProperty("candidates", out var cands) && cands.GetArrayLength() > 0)
                {
                    var content = cands[0].GetProperty("content");
                    var parts = content.GetProperty("parts");
                    if (parts.GetArrayLength() > 0)
                    {
                        return parts[0].GetProperty("text").GetString();
                    }
                }
            }
            else
            {
                if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    if (choices[0].TryGetProperty("delta", out var delta) && delta.TryGetProperty("content", out var content))
                    {
                        return content.GetString();
                    }
                }
            }
        }
        catch
        {
        }
        return null;
    }

    public async Task<ToolCallResponse> ExecuteToolAsync(ToolCallRequest request, string workspaceFolder, CancellationToken cancellationToken = default)
    {
        try
        {
            switch (request.ToolName.ToLowerInvariant())
            {
                case "readfile":
                    string readPath = ResolvePath(workspaceFolder, request.Arguments["path"]?.ToString() ?? "");
                    if (!File.Exists(readPath)) return new ToolCallResponse(request.Id, false, "", "File not found");
                    string content = await File.ReadAllTextAsync(readPath, cancellationToken);
                    return new ToolCallResponse(request.Id, true, content);

                case "writefile":
                    string writePath = ResolvePath(workspaceFolder, request.Arguments["path"]?.ToString() ?? "");
                    string writeContent = request.Arguments["content"]?.ToString() ?? "";
                    string? dir = Path.GetDirectoryName(writePath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    await File.WriteAllTextAsync(writePath, writeContent, cancellationToken);
                    return new ToolCallResponse(request.Id, true, $"File written successfully: {writePath}");

                case "applydiff":
                    string patchPath = ResolvePath(workspaceFolder, request.Arguments["path"]?.ToString() ?? "");
                    string patchContent = request.Arguments["diff"]?.ToString() ?? "";
                    await File.WriteAllTextAsync(patchPath, patchContent, cancellationToken);
                    return new ToolCallResponse(request.Id, true, $"Patch applied to: {patchPath}");

                case "searchfiles":
                    string query = request.Arguments["query"]?.ToString() ?? "";
                    var matches = new List<string>();
                    foreach (string file in Directory.EnumerateFiles(workspaceFolder, "*.*", SearchOption.AllDirectories))
                    {
                        if (Path.GetFileName(file).Contains(query, StringComparison.OrdinalIgnoreCase))
                        {
                            matches.Add(file);
                            if (matches.Count >= 20) break;
                        }
                    }
                    return new ToolCallResponse(request.Id, true, string.Join(Environment.NewLine, matches));

                case "runsandboxedcommand":
                    string cmd = request.Arguments["command"]?.ToString() ?? "";
                    var (stdout, stderr, code) = await RunCommandAsync(cmd, workspaceFolder, cancellationToken);
                    return new ToolCallResponse(request.Id, code == 0, stdout, code != 0 ? stderr : null);

                default:
                    return new ToolCallResponse(request.Id, false, "", $"Unknown tool: {request.ToolName}");
            }
        }
        catch (Exception ex)
        {
            return new ToolCallResponse(request.Id, false, "", ex.Message);
        }
    }

    public async IAsyncEnumerable<string> StreamHeadlessCliAsync(
        string cliName,
        string prompt,
        string workspaceFolder,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string nodeGlobal = _bootstrapper.NodeGlobalPath;
        string cliExecutable = cliName.ToLowerInvariant() switch
        {
            "claude" => Path.Combine(nodeGlobal, "claude.cmd"),
            "codex" => Path.Combine(nodeGlobal, "codex.cmd"),
            _ => Path.Combine(nodeGlobal, $"{cliName}.cmd")
        };

        if (!File.Exists(cliExecutable)) cliExecutable = cliName;

        var psi = new ProcessStartInfo
        {
            FileName = cliExecutable,
            Arguments = $"--print \"{prompt.Replace("\"", "\\\"")}\"",
            WorkingDirectory = workspaceFolder,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.Environment["ANTHROPIC_BASE_URL"] = _proxyManager.ProxyBaseUrl;
        psi.Environment["OPENAI_BASE_URL"] = $"{_proxyManager.ProxyBaseUrl}/v1";
        psi.Environment["OPENAI_API_BASE"] = $"{_proxyManager.ProxyBaseUrl}/v1";

        using var process = Process.Start(psi);
        if (process == null)
        {
            yield return $"Error: Could not start {cliName} CLI.";
            yield break;
        }

        using var reader = process.StandardOutput;
        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(cancellationToken);
            if (line != null) yield return line + Environment.NewLine;
        }

        await process.WaitForExitAsync(cancellationToken);
    }

    private static string ResolvePath(string root, string relative)
    {
        if (Path.IsPathRooted(relative)) return relative;
        return Path.GetFullPath(Path.Combine(root, relative));
    }

    private static async Task<(string stdout, string stderr, int code)> RunCommandAsync(string command, string workingDir, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c {command}",
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi);
        if (proc == null) return ("", "Could not start process", -1);

        string outText = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
        string errText = await proc.StandardError.ReadToEndAsync(cancellationToken);
        await proc.WaitForExitAsync(cancellationToken);

        return (outText, errText, proc.ExitCode);
    }

    private static string? ExtractDeltaContent(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                if (choice.TryGetProperty("delta", out var delta) && delta.TryGetProperty("content", out var content))
                {
                    return content.GetString();
                }
            }
        }
        catch
        {
        }
        return null;
    }
}
