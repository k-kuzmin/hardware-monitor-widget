using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using HardwareMonitorWidget.Infrastructure;
using HardwareMonitorWidget.Infrastructure.Win32;
using HardwareMonitorWidget.ViewModels;

namespace HardwareMonitorWidget;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly WindowPositionService _positionService;

    public MainWindow(MainViewModel viewModel, Func<Window, WindowPositionService> positionServiceFactory)
    {
        InitializeComponent();

        _positionService = positionServiceFactory(this);
        _viewModel = viewModel;
        DataContext = _viewModel;

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _positionService.TryRestore();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (!Win32Api.SetWindowPos(hwnd, Win32Api.HWND_BOTTOM, 0, 0, 0, 0, Win32Api.SWP_NOSIZE | Win32Api.SWP_NOMOVE | Win32Api.SWP_NOACTIVATE))
        {
            var error = Marshal.GetLastWin32Error();
            Debug.WriteLine($"[HardwareMonitor] SetWindowPos завершился с ошибкой: Win32 код {error}");
        }

        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HardwareMonitor] Ошибка инициализации: {ex}");
            MessageBox.Show(
                $"Ошибка запуска: {ex.Message}",
                "Hardware Monitor Widget",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Application.Current.Shutdown(1);
        }
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        _positionService.Save();

        try
        {
            await _viewModel.DisposeAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HardwareMonitor] Ошибка завершения: {ex}");
        }
    }

    private void OnRootMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
            _positionService.SnapToEdgesAndSave();
        }
    }
}