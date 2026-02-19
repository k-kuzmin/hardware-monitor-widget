using System.Diagnostics;
using System.Windows;
using HardwareMonitorWidget.Services;
using HardwareMonitorWidget.Services.Hardware;
using HardwareMonitorWidget.Services.Hardware.Readers;
using HardwareMonitorWidget.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace HardwareMonitorWidget;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ARCH-07: глобальная сеть для неперехваченных исключений из async void и WPF-потока
        DispatcherUnhandledException += (_, args) =>
        {
            Debug.WriteLine($"[HardwareMonitor] Необработанное исключение: {args.Exception}");
            args.Handled = true;
            MessageBox.Show(
                $"Критическая ошибка: {args.Exception.Message}",
                "Hardware Monitor Widget",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        };

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        _serviceProvider.GetRequiredService<MainWindow>().Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Порядок регистрации = порядок отображения метрик в виджете
        services.AddSingleton<IMetricReader, CpuLoadReader>();
        services.AddSingleton<IMetricReader, CpuTempReader>();
        services.AddSingleton<IMetricReader, GpuLoadReader>();
        services.AddSingleton<IMetricReader, GpuTempReader>();
        services.AddSingleton<IMetricReader, RamLoadReader>();

        services.AddSingleton<IHardwareMonitorService, LibreHardwareMonitorService>();
        services.AddSingleton<IStartupRegistrationService, TaskSchedulerStartupRegistrationService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}

