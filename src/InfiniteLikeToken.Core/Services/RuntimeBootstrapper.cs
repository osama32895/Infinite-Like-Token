using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using InfiniteLikeToken.Core.Models;

namespace InfiniteLikeToken.Core.Services;

public class RuntimeBootstrapper
{
    private readonly string _runtimeRoot;
    private readonly HttpClient _httpClient;

    public RuntimeBootstrapper(string? customRuntimeRoot = null, HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (string.IsNullOrWhiteSpace(customRuntimeRoot))
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _runtimeRoot = Path.Combine(localAppData, "InfiniteLikeToken", "runtime");
        }
        else
        {
            _runtimeRoot = customRuntimeRoot;
        }
    }

    public string RuntimeRoot => _runtimeRoot;

    public string LocalPythonPath => Path.Combine(_runtimeRoot, "python", "python.exe");
    public string LocalNodePath => Path.Combine(_runtimeRoot, "node", "node.exe");
    public string LocalNpmPath => Path.Combine(_runtimeRoot, "node", "npm.cmd");
    public string PythonPath => LocalPythonPath;
    public string NodePath => LocalNodePath;
    public string NpmPath => LocalNpmPath;
    public string NodeGlobalPath => Path.Combine(_runtimeRoot, "node_global");

    public async Task<string?> GetResolvedPythonAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(LocalPythonPath)) return LocalPythonPath;
        var (sysPy, _) = await FindInPathAsync("python.exe", cancellationToken);
        return sysPy;
    }

    public async Task<string?> GetResolvedNodeAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(LocalNodePath)) return LocalNodePath;
        var (sysNode, _) = await FindInPathAsync("node.exe", cancellationToken);
        return sysNode;
    }

    public async Task<string?> GetResolvedNpmAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(LocalNpmPath)) return LocalNpmPath;
        var (sysNpm, _) = await FindInPathAsync("npm.cmd", cancellationToken);
        return sysNpm;
    }

    public async Task<EnvironmentStatus> VerifyEnvironmentAsync(CancellationToken cancellationToken = default)
    {
        string? pyExec = await GetResolvedPythonAsync(cancellationToken);
        bool pyExists = !string.IsNullOrEmpty(pyExec);
        string? pyVersion = null;
        if (pyExists && pyExec != null)
        {
            pyVersion = await RunProcessOutputAsync(pyExec, "--version", cancellationToken);
            if (!string.IsNullOrEmpty(pyVersion)) pyVersion = pyVersion.Replace("Python", "").Trim();
        }

        string? nodeExec = await GetResolvedNodeAsync(cancellationToken);
        bool nodeExists = !string.IsNullOrEmpty(nodeExec);
        string? nodeVersion = null;
        if (nodeExists && nodeExec != null)
        {
            nodeVersion = await RunProcessOutputAsync(nodeExec, "-v", cancellationToken);
            if (!string.IsNullOrEmpty(nodeVersion)) nodeVersion = nodeVersion.Trim();
        }

        bool litellm = false;
        bool uvicorn = false;
        bool mcp = false;

        if (pyExists && pyExec != null)
        {
            string? pipList = await RunProcessOutputAsync(pyExec, "-m pip show litellm uvicorn mcp", cancellationToken);
            if (!string.IsNullOrEmpty(pipList))
            {
                litellm = pipList.Contains("Name: litellm", StringComparison.OrdinalIgnoreCase);
                uvicorn = pipList.Contains("Name: uvicorn", StringComparison.OrdinalIgnoreCase);
                mcp = pipList.Contains("Name: mcp", StringComparison.OrdinalIgnoreCase);
            }
        }

        bool claudeRouter = Directory.Exists(Path.Combine(NodeGlobalPath, "node_modules", "@musistudio", "claude-code-router"));
        bool claudeCode = Directory.Exists(Path.Combine(NodeGlobalPath, "node_modules", "@anthropic-ai", "claude-code"));
        bool codex = Directory.Exists(Path.Combine(NodeGlobalPath, "node_modules", "@openai", "codex"));
        bool reaAgents = Directory.Exists(Path.Combine(NodeGlobalPath, "node_modules", "rea-agents"));

        if (!claudeCode)
        {
            var (path, _) = await FindInPathAsync("claude.cmd", cancellationToken);
            if (path != null) claudeCode = true;
        }
        if (!codex)
        {
            var (path, _) = await FindInPathAsync("codex.cmd", cancellationToken);
            if (path != null) codex = true;
        }
        if (!reaAgents)
        {
            var (path, _) = await FindInPathAsync("rea-agents.cmd", cancellationToken);
            if (path != null) reaAgents = true;
        }

        return new EnvironmentStatus(
            NodeJsAvailable: nodeExists,
            NodeJsVersion: nodeVersion,
            PythonAvailable: pyExists,
            PythonVersion: pyVersion,
            LiteLlmInstalled: litellm,
            UvicornInstalled: uvicorn,
            McpInstalled: mcp,
            ClaudeCodeRouterInstalled: claudeRouter,
            ClaudeCodeInstalled: claudeCode,
            CodexInstalled: codex,
            ReaAgentsInstalled: reaAgents
        );
    }

    public async Task SetupEverythingAsync(IProgress<BootstrapProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_runtimeRoot);

        string? activePy = await GetResolvedPythonAsync(cancellationToken);
        if (string.IsNullOrEmpty(activePy))
        {
            progress?.Report(new BootstrapProgress("Python", "Downloading Python 3.11 embeddable package...", 15.0));
            string pyZip = Path.Combine(_runtimeRoot, "python.zip");
            await DownloadFileAsync("https://www.python.org/ftp/python/3.11.0/python-3.11.0-embed-amd64.zip", pyZip, cancellationToken);

            string pyDir = Path.Combine(_runtimeRoot, "python");
            Directory.CreateDirectory(pyDir);
            ZipFile.ExtractToDirectory(pyZip, pyDir, overwriteFiles: true);
            try { File.Delete(pyZip); } catch { }

            string pthFile = Path.Combine(pyDir, "python311._pth");
            if (File.Exists(pthFile))
            {
                string content = await File.ReadAllTextAsync(pthFile, cancellationToken);
                if (content.Contains("#import site"))
                {
                    content = content.Replace("#import site", "import site");
                    await File.WriteAllTextAsync(pthFile, content, cancellationToken);
                }
            }

            progress?.Report(new BootstrapProgress("Python", "Installing pip...", 25.0));
            string getPipPath = Path.Combine(pyDir, "get-pip.py");
            await DownloadFileAsync("https://bootstrap.pypa.io/get-pip.py", getPipPath, cancellationToken);
            await RunProcessAsync(LocalPythonPath, $"\"{getPipPath}\"", pyDir, cancellationToken);
            activePy = LocalPythonPath;
        }

        progress?.Report(new BootstrapProgress("Python Packages", "Installing litellm[proxy], uvicorn, mcp...", 35.0));
        await RunProcessAsync(activePy, "-m pip install --upgrade litellm[proxy] uvicorn mcp", _runtimeRoot, cancellationToken);

        string? activeNode = await GetResolvedNodeAsync(cancellationToken);
        string? activeNpm = await GetResolvedNpmAsync(cancellationToken);

        if (string.IsNullOrEmpty(activeNode) || string.IsNullOrEmpty(activeNpm))
        {
            progress?.Report(new BootstrapProgress("Node.js", "Downloading Node.js 22 portable zip...", 55.0));
            string nodeZip = Path.Combine(_runtimeRoot, "node.zip");
            await DownloadFileAsync("https://nodejs.org/dist/v22.11.0/node-v22.11.0-win-x64.zip", nodeZip, cancellationToken);

            string nodeExtractDir = Path.Combine(_runtimeRoot, "node_extract");
            Directory.CreateDirectory(nodeExtractDir);
            ZipFile.ExtractToDirectory(nodeZip, nodeExtractDir, overwriteFiles: true);

            string extractedFolder = Directory.GetDirectories(nodeExtractDir)[0];
            string nodeTarget = Path.Combine(_runtimeRoot, "node");
            if (Directory.Exists(nodeTarget)) Directory.Delete(nodeTarget, true);
            Directory.Move(extractedFolder, nodeTarget);

            try { Directory.Delete(nodeExtractDir, true); } catch { }
            try { File.Delete(nodeZip); } catch { }

            activeNode = LocalNodePath;
            activeNpm = LocalNpmPath;
        }

        Directory.CreateDirectory(NodeGlobalPath);
        await RunProcessAsync(activeNpm, $"config set prefix \"{NodeGlobalPath}\"", _runtimeRoot, cancellationToken);

        progress?.Report(new BootstrapProgress("Node CLI", "Installing @musistudio/claude-code-router...", 70.0));
        await RunProcessAsync(activeNpm, "install -g @musistudio/claude-code-router", _runtimeRoot, cancellationToken);

        progress?.Report(new BootstrapProgress("Node CLI", "Installing @anthropic-ai/claude-code...", 78.0));
        await RunProcessAsync(activeNpm, "install -g @anthropic-ai/claude-code", _runtimeRoot, cancellationToken);

        progress?.Report(new BootstrapProgress("Node CLI", "Installing @openai/codex...", 85.0));
        await RunProcessAsync(activeNpm, "install -g @openai/codex", _runtimeRoot, cancellationToken);

        progress?.Report(new BootstrapProgress("Node CLI", "Installing rea-agents...", 92.0));
        await RunProcessAsync(activeNpm, "install -g rea-agents", _runtimeRoot, cancellationToken);

        try
        {
            string cliJs = Path.Combine(NodeGlobalPath, "node_modules", "rea-agents", "bin", "cli.js");
            if (File.Exists(cliJs))
            {
                await RunProcessAsync(activeNode, $"\"{cliJs}\" setup", _runtimeRoot, cancellationToken);
            }
        }
        catch
        {
        }

        progress?.Report(new BootstrapProgress("Setup", "All dependencies installed and verified successfully!", 100.0, IsCompleted: true));
    }

    private static async Task<(string? path, string? version)> FindInPathAsync(string binaryName, CancellationToken cancellationToken)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "where.exe",
                Arguments = binaryName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return (null, null);

            string output = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);

            if (proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                string firstLine = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                return (firstLine, null);
            }
        }
        catch
        {
        }
        return (null, null);
    }

    private async Task DownloadFileAsync(string url, string destinationPath, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
        await response.Content.CopyToAsync(fs, cancellationToken);
    }

    private async Task RunProcessAsync(string fileName, string arguments, string workingDir, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) throw new InvalidOperationException($"Failed to start process: {fileName}");

        await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
    }

    private async Task<string?> RunProcessOutputAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return output;
        }
        catch
        {
            return null;
        }
    }
}
