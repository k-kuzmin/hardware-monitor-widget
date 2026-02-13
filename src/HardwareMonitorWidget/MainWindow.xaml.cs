using System.Windows;
using System.Windows.Input;
using HardwareMonitorWidget.Services;
using HardwareMonitorWidget.ViewModels;

namespace HardwareMonitorWidget;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        var hardwareMonitorService = new LibreHardwareMonitorService();
        var startupRegistrationService = new TaskSchedulerStartupRegistrationService();

        _viewModel = new MainViewModel(hardwareMonitorService, startupRegistrationService);
        DataContext = _viewModel;

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
        Closed -= OnClosed;
        await _viewModel.DisposeAsync();
    }

    private void OnRootMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}