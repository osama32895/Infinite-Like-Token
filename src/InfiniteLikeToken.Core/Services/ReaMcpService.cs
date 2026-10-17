using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using InfiniteLikeToken.Core.Models;

namespace InfiniteLikeToken.Core.Services;

public class ReaMcpService
{
    private readonly RuntimeBootstrapper _bootstrapper;
    private readonly BuiltInAgentService _agentService;

    public ReaMcpService(RuntimeBootstrapper bootstrapper, BuiltInAgentService agentService)
    {
        _bootstrapper = bootstrapper;
        _agentService = agentService;
    }

    public async Task<ReaTargetArtifact> InspectArtifactAsync(string targetPath, CancellationToken cancellationToken = default)
    {
        bool isDir = Directory.Exists(targetPath);
        bool isFile = File.Exists(targetPath);

        if (!isDir && !isFile)
        {
            throw new FileNotFoundException($"Target path not found: {targetPath}");
        }

        string fileName = Path.GetFileName(targetPath);
        string ext = isFile ? Path.GetExtension(targetPath).ToLowerInvariant() : "[Directory]";
        long size = isFile ? new FileInfo(targetPath).Length : 0;

        var sections = new List<string>();
        var imports = new List<string>();
        var exports = new List<string>();
        var stringsList = new List<string>();
        string decompiledPreview = "";
        double entropy = 0.0;

        if (isFile)
        {
            byte[] bytes = await File.ReadAllBytesAsync(targetPath, cancellationToken);
            entropy = CalculateEntropy(bytes);
            sections.Add($".text (Code Executable) - {bytes.Length:N0} bytes");
            sections.Add($".rdata (Constants & Strings) - Entropy: {entropy:F2}");

            if (ext == ".dll" || ext == ".exe")
            {
                if (await IsDotNetAssemblyAsync(targetPath))
                {
                    sections.Add(".NET CIL / MSIL Managed Assembly Metadata");
                    decompiledPreview = await DecompileWithILSpyAsync(targetPath, cancellationToken);
                }
                else
                {
                    sections.Add("Native PE Header (x86/x64)");
                    decompiledPreview = await ExtractPeMetadataAsync(targetPath, cancellationToken);
                }
            }
            else if (ext == ".apk")
            {
                sections.Add("Android Package Archive (AndroidManifest.xml & classes.dex)");
                decompiledPreview = await ExtractApkMetadataAsync(targetPath, cancellationToken);
            }
            else
            {
                decompiledPreview = await ExtractStringsSummaryAsync(bytes, stringsList);
            }
        }
        else
        {
            foreach (string file in Directory.EnumerateFiles(targetPath, "*.*", SearchOption.AllDirectories))
            {
                sections.Add(Path.GetRelativePath(targetPath, file));
            }
            decompiledPreview = $"Directory workspace analyzed containing {sections.Count} files.";
        }

        return new ReaTargetArtifact(
            targetPath,
            fileName,
            ext,
            size,
            isDir,
            entropy,
            sections,
            imports,
            exports,
            stringsList,
            decompiledPreview
        );
    }

    public async Task<string> AnalyzeArtifactAsync(string targetFilePath, string actionPreset, CancellationToken cancellationToken = default)
    {
        var artifact = await InspectArtifactAsync(targetFilePath, cancellationToken);
        var sb = new StringBuilder();
        await AnalyzeWithAiAsync(artifact, actionPreset, new Progress<string>(token => sb.Append(token)), cancellationToken);
        return sb.ToString();
    }

    public async Task AnalyzeWithAiAsync(
        ReaTargetArtifact artifact,
        string analysisGoal,
        IProgress<string> streamOutput,
        CancellationToken cancellationToken = default)
    {
        string prompt = $"[REA Studio Deep Analysis Request]\n" +
                        $"Target: {artifact.FileName} ({artifact.Extension}, {artifact.FileSize:N0} bytes)\n" +
                        $"Analysis Goal / Preset: {analysisGoal}\n\n" +
                        $"Extracted Technical Evidence & Metadata:\n" +
                        $"Entropy: {artifact.Entropy:F2}\n" +
                        $"Sections/Modules: {string.Join(", ", artifact.DiscoveredSections)}\n\n" +
                        $"Decompiled Preview / Content:\n{artifact.DecompiledPreview}\n\n" +
                        $"Please provide a comprehensive, rigorous reverse-engineering report, architectural breakdown, potential vulnerability assessment, and clean reconstructed C# or Python source code.";

        await foreach (var token in _agentService.StreamChatAsync(prompt, Path.GetDirectoryName(artifact.Path) ?? Environment.CurrentDirectory, "tier-rea", null, cancellationToken))
        {
            streamOutput.Report(token);
        }
    }

    private static double CalculateEntropy(byte[] data)
    {
        if (data.Length == 0) return 0;
        var freq = new int[256];
        foreach (byte b in data) freq[b]++;
        double entropy = 0.0;
        foreach (int count in freq)
        {
            if (count > 0)
            {
                double p = (double)count / data.Length;
                entropy -= p * Math.Log2(p);
            }
        }
        return entropy;
    }

    private static Task<bool> IsDotNetAssemblyAsync(string filePath)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            string text = Encoding.ASCII.GetString(bytes);
            return Task.FromResult(text.Contains("mscorlib") || text.Contains("System.Runtime") || text.Contains("BSJB"));
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private static async Task<string> DecompileWithILSpyAsync(string filePath, CancellationToken cancellationToken)
    {
        var (stdout, _, code) = await RunCliAsync("ilspycmd", $"\"{filePath}\" -p", cancellationToken);
        if (code == 0 && !string.IsNullOrWhiteSpace(stdout)) return stdout;
        byte[] bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        var list = new List<string>();
        return await ExtractStringsSummaryAsync(bytes, list);
    }

    private static async Task<string> ExtractPeMetadataAsync(string filePath, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        try
        {
            byte[] bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
            sb.AppendLine($"PE File size: {bytes.Length:N0} bytes");
            var list = new List<string>();
            sb.AppendLine(await ExtractStringsSummaryAsync(bytes, list));
        }
        catch (Exception ex)
        {
            sb.AppendLine($"PE error: {ex.Message}");
        }
        return sb.ToString();
    }

    private static Task<string> ExtractApkMetadataAsync(string filePath, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        try
        {
            using var zip = ZipFile.OpenRead(filePath);
            foreach (var entry in zip.Entries)
            {
                sb.AppendLine($"  - {entry.FullName} ({entry.Length:N0} bytes)");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"APK error: {ex.Message}");
        }
        return Task.FromResult(sb.ToString());
    }

    private static Task<string> ExtractStringsSummaryAsync(byte[] bytes, List<string> store)
    {
        var sb = new StringBuilder();
        var current = new StringBuilder();
        int found = 0;
        for (int i = 0; i < bytes.Length && found < 50; i++)
        {
            byte b = bytes[i];
            if (b >= 32 && b <= 126) current.Append((char)b);
            else
            {
                if (current.Length >= 6)
                {
                    string s = current.ToString();
                    store.Add(s);
                    sb.AppendLine($"  [Str] {s}");
                    found++;
                }
                current.Clear();
            }
        }
        return Task.FromResult(sb.ToString());
    }

    private static async Task<(string stdout, string stderr, int code)> RunCliAsync(string fileName, string args, CancellationToken cancellationToken)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p == null) return ("", "Not started", -1);
            string o = await p.StandardOutput.ReadToEndAsync(cancellationToken);
            string e = await p.StandardError.ReadToEndAsync(cancellationToken);
            await p.WaitForExitAsync(cancellationToken);
            return (o, e, p.ExitCode);
        }
        catch (Exception ex)
        {
            return ("", ex.Message, -1);
        }
    }
}
