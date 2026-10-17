using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InfiniteLikeToken.Core.Services;

namespace InfiniteLikeToken.UI.ViewModels;

public partial class ReverseEngViewModel : ObservableObject
{
    private readonly ReaMcpService _reaService;

    [ObservableProperty]
    private string _targetFilePath = string.Empty;

    [ObservableProperty]
    private bool _isAnalyzing;

    [ObservableProperty]
    private string _statusMessage = "Drag & drop any .exe, .dll, .apk, .wasm or click to select.";

    [ObservableProperty]
    private ObservableCollection<string> _discoveredModules = new();

    [ObservableProperty]
    private string _reconstructionOutput = "// REA Studio Reconstructed Source Code & Evidence will appear here.";

    public ReverseEngViewModel(ReaMcpService reaService)
    {
        _reaService = reaService;
    }

    [RelayCommand]
    public void BrowseFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Target Binary for REA Decompilation",
            Filter = "All Supported Binaries (*.dll;*.exe;*.apk;*.wasm)|*.dll;*.exe;*.apk;*.wasm|All Files (*.*)|*.*"
        };
        if (dialog.ShowDialog() == true)
        {
            LoadTarget(dialog.FileName);
        }
    }

    public void LoadTarget(string filePath)
    {
        TargetFilePath = filePath;
        StatusMessage = $"Target loaded: {Path.GetFileName(filePath)}";

        DiscoveredModules.Clear();
        DiscoveredModules.Add($"[Assembly/Binary] {Path.GetFileName(filePath)}");
        DiscoveredModules.Add("  -> .text (Executable Code)");
        DiscoveredModules.Add("  -> .rdata (Read-Only Data & Strings)");
        DiscoveredModules.Add("  -> .data (Initialized Globals)");
        DiscoveredModules.Add("  -> .pdata (Exception Tables)");
    }

    [RelayCommand]
    public async Task RunPresetAsync(string actionPreset)
    {
        if (string.IsNullOrWhiteSpace(TargetFilePath) || IsAnalyzing) return;

        IsAnalyzing = true;
        StatusMessage = $"Running REA Action: {actionPreset}...";
        ReconstructionOutput = "Decompiling and performing deep static analysis...";

        try
        {
            string result = await _reaService.AnalyzeArtifactAsync(TargetFilePath, actionPreset);
            ReconstructionOutput = result;
            StatusMessage = "Analysis and decompilation complete.";
        }
        catch (Exception ex)
        {
            ReconstructionOutput = $"Error during reverse engineering: {ex.Message}";
            StatusMessage = "Analysis failed.";
        }
        finally
        {
            IsAnalyzing = false;
        }
    }
}
