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
    private static readonly Brush[] BarBrushPalette = CreateBarBrushPalette();
    private static readonly Brush[] TextBrushPalette = CreateTextBrushPalette();

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
    private string _startupStatus = "Автозапуск: настройка";

    [ObservableProperty]
    private string _startupStatusDetails = "Инициализация автозапуска...";

    [ObservableProperty]
    private Visibility _startupStatusVisible = Visibility.Collapsed;

    public MainViewModel(IHardwareMonitorService hardwareMonitorService, IStartupRegistrationService startupRegistrationService)
    {
        _hardwareMonitorService = hardwareMonitorService;
        _startupRegistrationService = startupRegistrationService;

        Metrics = new ObservableCollection<MetricItem>
        {
            new("CPU Load", "%"),
            new("CPU Temp", "°C"),
            new("GPU Load", "%"),
            new("GPU Temp", "°C"),
            new("RAM Load", "%")
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
            SetTargetsFromSnapshot(snapshot);

            GpuDisplayName = $"GPU: {snapshot.GpuName}";
        });
    }

    private async Task AnimationLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await Application.Current.Dispatcher.InvokeAsync(() => ApplyInterpolatedValues(DateTime.UtcNow));
        }
    }

    private void SetTargetsFromSnapshot(HardwareSnapshot snapshot)
    {
        _animationStartUtc = DateTime.UtcNow;

        SetNewTarget(0, snapshot.CpuLoad);
        SetNewTarget(1, snapshot.CpuTemperature);
        SetNewTarget(2, snapshot.GpuLoad);
        SetNewTarget(3, snapshot.GpuTemperature);
        SetNewTarget(4, snapshot.RamLoad);
    }

    private void SetNewTarget(int index, double newTarget)
    {
        _startValues[index] = _currentValues[index];
        _targetValues[index] = Math.Clamp(newTarget, 0, 100);
    }

    private void ApplyInterpolatedValues(DateTime nowUtc)
    {
        var progress = (nowUtc - _animationStartUtc).TotalMilliseconds / AnimationDuration.TotalMilliseconds;
        var t = Math.Clamp(progress, 0, 1);

        for (var index = 0; index < Metrics.Count; index++)
        {
            _currentValues[index] = _startValues[index] + ((_targetValues[index] - _startValues[index]) * t);
            Metrics[index].Value = _currentValues[index];

            var brush = GetBarBrush(_currentValues[index]);
            if (!ReferenceEquals(Metrics[index].BarBrush, brush))
            {
                Metrics[index].BarBrush = brush;
            }

            var textBrush = GetTextBrush(_currentValues[index]);
            if (!ReferenceEquals(Metrics[index].TextBrush, textBrush))
            {
                Metrics[index].TextBrush = textBrush;
            }
        }
    }

    private static Brush GetBarBrush(double value)
    {
        var paletteIndex = (int)Math.Round(Math.Clamp(value, 0d, 100d), MidpointRounding.AwayFromZero);
        return BarBrushPalette[paletteIndex];
    }

    private static Brush GetTextBrush(double value)
    {
        var paletteIndex = (int)Math.Round(Math.Clamp(value, 0d, 100d), MidpointRounding.AwayFromZero);
        return TextBrushPalette[paletteIndex];
    }

    private static Brush[] CreateBarBrushPalette()
    {
        var palette = new Brush[101];

        for (var i = 0; i <= 100; i++)
        {
            palette[i] = CreateProgressiveBarBrush(i);
        }

        return palette;
    }

    private static Brush[] CreateTextBrushPalette()
    {
        var palette = new Brush[101];

        for (var i = 0; i <= 100; i++)
        {
            var color = CreateProgressiveTextColor(i);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            palette[i] = brush;
        }

        return palette;
    }

    private static Color CreateProgressiveTextColor(double value)
    {
        var normalized = Math.Clamp(value / 100d, 0d, 1d);

        var green = Color.FromRgb(30, 255, 102);
        var lime = Color.FromRgb(166, 240, 42);
        var yellow = Color.FromRgb(255, 200, 70);
        var red = Color.FromRgb(255, 46, 79);

        if (normalized < 0.45)
        {
            return LerpColor(green, lime, normalized / 0.45);
        }

        if (normalized < 0.8)
        {
            return LerpColor(lime, yellow, (normalized - 0.45) / 0.35);
        }

        return LerpColor(yellow, red, (normalized - 0.8) / 0.2);
    }

    private static Color LerpColor(Color start, Color end, double t)
    {
        var clampedT = Math.Clamp(t, 0d, 1d);

        byte Interpolate(byte left, byte right)
        {
            return (byte)Math.Round(left + ((right - left) * clampedT), MidpointRounding.AwayFromZero);
        }

        return Color.FromRgb(
            Interpolate(start.R, end.R),
            Interpolate(start.G, end.G),
            Interpolate(start.B, end.B));
    }

    private static Brush CreateProgressiveBarBrush(double value)
    {
        var normalized = Math.Clamp(value / 100d, 0d, 1d);

        var green = Color.FromRgb(30, 255, 102);
        var lime = Color.FromRgb(166, 240, 42);
        var yellow = Color.FromRgb(255, 200, 70);
        var red = Color.FromRgb(255, 46, 79);

        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5)
        };

        if (normalized < 0.45)
        {
            brush.GradientStops.Add(new GradientStop(green, 0));
            brush.GradientStops.Add(new GradientStop(lime, 1));
        }
        else if (normalized < 0.8)
        {
            brush.GradientStops.Add(new GradientStop(green, 0));
            brush.GradientStops.Add(new GradientStop(lime, 0.6));
            brush.GradientStops.Add(new GradientStop(yellow, 1));
        }
        else
        {
            brush.GradientStops.Add(new GradientStop(green, 0));
            brush.GradientStops.Add(new GradientStop(lime, 0.45));
            brush.GradientStops.Add(new GradientStop(yellow, 0.72));
            brush.GradientStops.Add(new GradientStop(red, 1));
        }

        brush.Freeze();
        return brush;
    }

    private async Task RegisterAutostartAsync()
    {
        var executablePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            StartupStatus = "Автозапуск: нет пути";
            StartupStatusDetails = "Не найден путь к исполняемому файлу приложения.";
            StartupStatusVisible = Visibility.Visible;
            return;
        }

        try
        {
            var registered = await _startupRegistrationService.EnsureMachineWideAutostartAsync(executablePath);
            if (registered)
            {
                // Успех - скрываем статус
                StartupStatusVisible = Visibility.Collapsed;
            }
            else
            {
                StartupStatus = "Автозапуск: нет прав";
                StartupStatusDetails = "Не удалось настроить автозапуск. Попробуйте запустить приложение от администратора.";
                StartupStatusVisible = Visibility.Visible;
            }
        }
        catch
        {
            StartupStatus = "Автозапуск: ошибка";
            StartupStatusDetails = "Произошла ошибка при регистрации автозапуска.";
            StartupStatusVisible = Visibility.Visible;
        }
    }
}
