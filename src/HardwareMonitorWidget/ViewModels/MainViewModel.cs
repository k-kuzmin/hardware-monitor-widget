using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using HardwareMonitorWidget.Models;
using HardwareMonitorWidget.Services;

namespace HardwareMonitorWidget.ViewModels;

public partial class MainViewModel : ObservableObject, IAsyncDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan AnimationDuration = TimeSpan.FromMilliseconds(700);

    private readonly IHardwareMonitorService _hardwareMonitorService;
    private readonly IStartupRegistrationService _startupRegistrationService;
    private readonly CancellationTokenSource _cts = new();

    private readonly double[] _currentValues;
    private readonly double[] _startValues;
    private readonly double[] _targetValues;
    private DateTime _animationStartUtc;

    private Task? _pollingTask;
    private Task? _animationTask;

    public ObservableCollection<MetricItem> Metrics { get; }

    [ObservableProperty]
    private string _gpuDisplayName = "GPU: поиск...";

    [ObservableProperty]
    private string _startupStatus = "Автозапуск: регистрация...";

    public MainViewModel(IHardwareMonitorService hardwareMonitorService, IStartupRegistrationService startupRegistrationService)
    {
        _hardwareMonitorService = hardwareMonitorService;
        _startupRegistrationService = startupRegistrationService;

        Metrics = new ObservableCollection<MetricItem>
        {
            new("CPU Load", "%"),
            new("CPU Temp", "°C"),
            new("RAM Load", "%"),
            new("GPU Load", "%"),
            new("GPU Temp", "°C")
        };

        _currentValues = new double[Metrics.Count];
        _startValues = new double[Metrics.Count];
        _targetValues = new double[Metrics.Count];
        _animationStartUtc = DateTime.UtcNow;
    }

    public async Task InitializeAsync()
    {
        await RegisterAutostartAsync();
        _pollingTask = Task.Run(() => PollLoopAsync(_cts.Token));
        _animationTask = Task.Run(() => AnimationLoopAsync(_cts.Token));
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();

        if (_pollingTask is not null)
        {
            try
            {
                await _pollingTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        if (_animationTask is not null)
        {
            try
            {
                await _animationTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _hardwareMonitorService.Dispose();
        _cts.Dispose();
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        await RefreshTargetsAsync(cancellationToken);

        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await RefreshTargetsAsync(cancellationToken);
        }
    }

    private async Task RefreshTargetsAsync(CancellationToken cancellationToken)
    {
        var snapshot = await Task.Run(_hardwareMonitorService.ReadSnapshot, cancellationToken);

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            SetNewTarget(0, snapshot.CpuLoad);
            SetNewTarget(1, snapshot.CpuTemperature);
            SetNewTarget(2, snapshot.RamLoad);
            SetNewTarget(3, snapshot.GpuLoad);
            SetNewTarget(4, snapshot.GpuTemperature);

            GpuDisplayName = $"GPU: {snapshot.GpuName}";
        });
    }

    private async Task AnimationLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(40));

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await Application.Current.Dispatcher.InvokeAsync(() => ApplyInterpolatedValues(DateTime.UtcNow));
        }
    }

    private void SetNewTarget(int index, double newTarget)
    {
        _startValues[index] = _currentValues[index];
        _targetValues[index] = Math.Clamp(newTarget, 0, 100);
        _animationStartUtc = DateTime.UtcNow;
    }

    private void ApplyInterpolatedValues(DateTime nowUtc)
    {
        var progress = (nowUtc - _animationStartUtc).TotalMilliseconds / AnimationDuration.TotalMilliseconds;
        var t = Math.Clamp(progress, 0, 1);

        for (var index = 0; index < Metrics.Count; index++)
        {
            _currentValues[index] = _startValues[index] + ((_targetValues[index] - _startValues[index]) * t);
            Metrics[index].Value = _currentValues[index];
            Metrics[index].BarBrush = new SolidColorBrush(GetGradientColor(_currentValues[index]));
        }
    }

    private static Color GetGradientColor(double value)
    {
        var normalized = Math.Clamp(value / 100d, 0d, 1d);
        var red = (byte)(255 * normalized);
        var green = (byte)(255 * (1 - normalized));
        return Color.FromRgb(red, green, 40);
    }

    private async Task RegisterAutostartAsync()
    {
        var executablePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            StartupStatus = "Автозапуск: путь к приложению не найден";
            return;
        }

        try
        {
            var registered = await _startupRegistrationService.EnsureMachineWideAutostartAsync(executablePath);
            StartupStatus = registered
                ? "Автозапуск: задача Task Scheduler настроена"
                : "Автозапуск: не удалось настроить (попробуйте запуск от администратора)";
        }
        catch
        {
            StartupStatus = "Автозапуск: ошибка регистрации";
        }
    }
}
