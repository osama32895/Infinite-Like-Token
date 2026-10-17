using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace InfiniteLiteToken.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableObject _currentView;

    [ObservableProperty]
    private string _activeViewName = "AgentChat";

    [ObservableProperty]
    private string _statusText = "Ready - Infinite Lite Token Local Proxy (127.0.0.1:4000)";

    public SetupWizardViewModel SetupWizardVM { get; }
    public KeyVaultViewModel KeyVaultVM { get; }
    public AgentChatViewModel AgentChatVM { get; }
    public ReverseEngViewModel ReverseEngVM { get; }
    public PluginStoreViewModel PluginStoreVM { get; }
    public StatusCenterViewModel StatusCenterVM { get; }

    public MainViewModel(
        SetupWizardViewModel setupWizardVM,
        KeyVaultViewModel keyVaultVM,
        AgentChatViewModel agentChatVM,
        ReverseEngViewModel reverseEngVM,
        PluginStoreViewModel pluginStoreVM,
        StatusCenterViewModel statusCenterVM)
    {
        SetupWizardVM = setupWizardVM;
        KeyVaultVM = keyVaultVM;
        AgentChatVM = agentChatVM;
        ReverseEngVM = reverseEngVM;
        PluginStoreVM = pluginStoreVM;
        StatusCenterVM = statusCenterVM;

        _currentView = agentChatVM;
    }

    [RelayCommand]
    private void Navigate(string destination)
    {
        ActiveViewName = destination;
        CurrentView = destination switch
        {
            "SetupWizard" => SetupWizardVM,
            "KeyVault" => KeyVaultVM,
            "AgentChat" => AgentChatVM,
            "ReverseEng" => ReverseEngVM,
            "PluginStore" => PluginStoreVM,
            "StatusCenter" => StatusCenterVM,
            _ => AgentChatVM
        };
    }
}