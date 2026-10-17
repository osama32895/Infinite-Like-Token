using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace InfiniteLikeToken.UI.ViewModels;

public partial class PluginItem : ObservableObject
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    [ObservableProperty] private string _status = "Install";
}

public partial class PluginStoreViewModel : ObservableObject
{
    public ObservableCollection<PluginItem> AvailablePlugins { get; } = new()
    {
        new PluginItem { Name = "Codex Full-Stack Supercharger", Description = "Advanced .NET and React code generation templates." },
        new PluginItem { Name = "REA Binary Deep Bridge", Description = "Connects Ghidra and ILSpy output directly to the AI." },
        new PluginItem { Name = "MCP GitHub Automator", Description = "Allows the AI to push, pull, and review PRs directly." }
    };

    [RelayCommand]
    public void TogglePlugin(PluginItem plugin)
    {
        plugin.Status = plugin.Status == "Install" ? "Enabled ✔️" : "Install";
    }
}
