using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InfiniteLiteToken.Core.Models;
using InfiniteLiteToken.Core.Services;

namespace InfiniteLiteToken.UI.ViewModels;

public partial class SetupWizardViewModel : ObservableObject
{
    private readonly RuntimeBootstrapper _bootstrapper;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "Ready to inspect and install dependencies.";

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private bool _pythonInstalled;

    [ObservableProperty]
    private bool _nodeInstalled;

    [ObservableProperty]
    private bool _litellmInstalled;

    [ObservableProperty]
    private bool _codingEngineInstalled;

    [ObservableProperty]
    private bool _reaToolkitInstalled;

    public SetupWizardViewModel(RuntimeBootstrapper bootstrapper)
    {
        _bootstrapper = bootstrapper;
        _ = CheckStatusAsync();
    }

    [RelayCommand]
    public async Task CheckStatusAsync()
    {
        try
        {
            var status = await _bootstrapper.VerifyEnvironmentAsync();
            PythonInstalled = status.PythonAvailable;
            NodeInstalled = status.NodeJsAvailable;
            LitellmInstalled = status.LiteLlmInstalled;
            CodingEngineInstalled = status.ClaudeCodeInstalled || status.CodexInstalled || status.ClaudeCodeRouterInstalled;
            ReaToolkitInstalled = status.ReaAgentsInstalled;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Verification error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task InstallEverythingAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ProgressValue = 0;
        StatusMessage = "Starting 1-Click Dependency Bootstrapper...";

        var progress = new Progress<BootstrapProgress>(p =>
        {
            StatusMessage = $"[{p.Component}] {p.Status}";
            ProgressValue = p.Percentage;
        });

        try
        {
            await _bootstrapper.SetupEverythingAsync(progress);
            await CheckStatusAsync();
            StatusMessage = "All environments successfully verified and operational!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Installation error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
