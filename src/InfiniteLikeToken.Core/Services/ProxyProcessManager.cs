using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using InfiniteLikeToken.Core.Models;

namespace InfiniteLikeToken.Core.Services;

/// <summary>
/// Manages the local LiteLLM proxy process, injects environment variables, and parses runtime logs.
/// </summary>
public class ProxyProcessManager : IDisposable
{
    private Process? _proxyProcess;
    private readonly RuntimeBootstrapper _bootstrapper;
    private readonly LiteLlmConfigBuilder _configBuilder;
    private readonly KeyVaultService _vaultService;

    public event Action<string>? LogReceived;
    public event Action<string, KeyHealthStatus>? KeyStatusUpdated;

    public bool IsRunning => _proxyProcess != null && !_proxyProcess.HasExited;
    public string ProxyBaseUrl => "http://127.0.0.1:4000";

    public ProxyProcessManager(RuntimeBootstrapper bootstrapper, LiteLlmConfigBuilder configBuilder, KeyVaultService vaultService)
    {
        _bootstrapper = bootstrapper;
        _configBuilder = configBuilder;
        _vaultService = vaultService;
    }

    public async Task StartProxyAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return;
        }

        string configPath = await _configBuilder.GenerateConfigYamlAsync(cancellationToken);

        string litellmExe = Path.Combine(_bootstrapper.RuntimeRoot, "python", "Scripts", "litellm.exe");
        string arguments;
        string fileName;

        if (File.Exists(litellmExe))
        {
            fileName = litellmExe;
            arguments = $"--config \"{configPath}\" --port 4000 --host 127.0.0.1";
        }
        else
        {
            // Fallback to python -m litellm.proxy.proxy_cli or system litellm
            fileName = File.Exists(_bootstrapper.PythonPath) ? _bootstrapper.PythonPath : "python";
            arguments = $"-m litellm --config \"{configPath}\" --port 4000 --host 127.0.0.1";
        }

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Add python and node paths to PATH
        string currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        string pyDir = Path.GetDirectoryName(_bootstrapper.PythonPath) ?? string.Empty;
        string pyScripts = Path.Combine(pyDir, "Scripts");
        string nodeDir = Path.GetDirectoryName(_bootstrapper.NodePath) ?? string.Empty;
        string nodeGlobal = _bootstrapper.NodeGlobalPath;

        psi.Environment["PATH"] = $"{pyDir};{pyScripts};{nodeDir};{nodeGlobal};{currentPath}";
        psi.Environment["PYTHONPATH"] = AppDomain.CurrentDomain.BaseDirectory;

        // Claude Code & Universal Agent standard overrides
        psi.Environment["ANTHROPIC_BASE_URL"] = ProxyBaseUrl;
        psi.Environment["OPENAI_BASE_URL"] = $"{ProxyBaseUrl}/v1";
        psi.Environment["OPENAI_API_BASE"] = $"{ProxyBaseUrl}/v1";

        _proxyProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };

        _proxyProcess.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                ParseLogLine(e.Data);
            }
        };

        _proxyProcess.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                ParseLogLine(e.Data);
            }
        };

        _proxyProcess.Start();
        _proxyProcess.BeginOutputReadLine();
        _proxyProcess.BeginErrorReadLine();

        // Allow proxy a moment to bind port
        await Task.Delay(1000, cancellationToken);
    }

    public void StopProxy()
    {
        try
        {
            if (_proxyProcess != null && !_proxyProcess.HasExited)
            {
                _proxyProcess.Kill(entireProcessTree: true);
                _proxyProcess.Dispose();
            }
        }
        catch
        {
        }
        finally
        {
            _proxyProcess = null;
        }
    }

    private void ParseLogLine(string line)
    {
        LogReceived?.Invoke(line);

        // Detect Rate Limits (429) and Cooldowns
        if (line.Contains("429") || line.Contains("RateLimitError") || line.Contains("RESOURCE_EXHAUSTED"))
        {
            var match = Regex.Match(line, @"key_nickname['""]?\s*:\s*['""]([^'""]+)");
            if (match.Success)
            {
                string nickname = match.Groups[1].Value;
                KeyStatusUpdated?.Invoke(nickname, KeyHealthStatus.Cooldown);
            }
        }
        // Detect Quota Exceeded
        else if (line.Contains("quota") || line.Contains("insufficient_quota"))
        {
            var match = Regex.Match(line, @"key_nickname['""]?\s*:\s*['""]([^'""]+)");
            if (match.Success)
            {
                string nickname = match.Groups[1].Value;
                KeyStatusUpdated?.Invoke(nickname, KeyHealthStatus.QuotaExhausted);
            }
        }
    }

    public void Dispose()
    {
        StopProxy();
    }
}
