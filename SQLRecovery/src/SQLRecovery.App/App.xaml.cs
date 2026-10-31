using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using SQLRecovery.App.Services;
using SQLRecovery.App.ViewModels;

namespace SQLRecovery.App;

public partial class App : Application
{
    private ServiceProvider? _services;

    public static IServiceProvider Services =>
        ((App)Current)._services ?? throw new InvalidOperationException("Service provider not initialized.");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var collection = new ServiceCollection();
        ConfigureServices(collection);
        _services = collection.BuildServiceProvider();

        var window = _services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<SettingsService>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        ShowFatal(e.ExceptionObject as Exception);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ShowFatal(e.Exception);
        e.Handled = true;
    }

    private static void ShowFatal(Exception? exception)
    {
        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SQLRecoveryTool",
                "crash.log");

            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, $"[{DateTime.Now:O}]{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Ignore logging failures.
        }

        MessageBox.Show(
            exception?.ToString() ?? "An unknown error occurred.",
            "SQL Recovery Tool - Unexpected error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
