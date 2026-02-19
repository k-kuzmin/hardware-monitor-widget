using System.Diagnostics;
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

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();

        _positionService = new WindowPositionService(this);
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
        // Установка окна позади всех остальных окон
        var hwnd = new WindowInteropHelper(this).Handle;
        Win32Api.SetWindowPos(hwnd, Win32Api.HWND_BOTTOM, 0, 0, 0, 0, Win32Api.SWP_NOSIZE | Win32Api.SWP_NOMOVE | Win32Api.SWP_NOACTIVATE);

        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            // ARCH-01: async void — исключения должны перехватываться здесь, иначе WPF молча крашает процесс
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

        // CQ-07: самоотписка от собственных событий бессмысленна — удалена
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