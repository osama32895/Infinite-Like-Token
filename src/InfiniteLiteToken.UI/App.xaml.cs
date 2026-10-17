using System;
using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using InfiniteLiteToken.Core.Services;
using InfiniteLiteToken.UI.ViewModels;

namespace InfiniteLiteToken.UI;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            MessageBox.Show($"Unhandled Exception: {args.ExceptionObject}", "Crash", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        base.OnStartup(e);

        try
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.DataContext = _serviceProvider.GetRequiredService<MainViewModel>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Startup Error: {ex.Message}\n\nInner: {ex.InnerException?.Message}\n\nStackTrace:\n{ex.StackTrace}", 
                "Initialization Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<HttpClient>();
        services.AddSingleton<KeyVaultService>();
        services.AddSingleton<RuntimeBootstrapper>();
        services.AddSingleton<LiteLlmConfigBuilder>();
        services.AddSingleton<ProxyProcessManager>();
        services.AddSingleton<TelemetryService>();
        services.AddSingleton<PluginManagerService>();
        services.AddSingleton<BuiltInAgentService>();
        services.AddSingleton<ReaMcpService>();
        services.AddSingleton<ChatSessionService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<SetupWizardViewModel>();
        services.AddSingleton<KeyVaultViewModel>();
        services.AddSingleton<AgentChatViewModel>();
        services.AddSingleton<ReverseEngViewModel>();
		services.AddSingleton<PluginStoreViewModel>();
        services.AddSingleton<StatusCenterViewModel>();

        // Views
        services.AddTransient<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.GetService<ProxyProcessManager>()?.Dispose();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}