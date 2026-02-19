using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using HardwareMonitorWidget.Models;
using HardwareMonitorWidget.Services;

namespace HardwareMonitorWidget.ViewModels;

public partial class MainViewModel : ObservableObject, IAsyncDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private readonly IHardwareMonitorService _hardwareMonitorService;
    private readonly MetricAnimator _animator = new();
    private readonly IStartupRegistrationService _startupRegistrationService;
    private readonly CancellationTokenSource _cts = new();

    private readonly double[] _currentValues;
    private readonly double[] _startValues;
    private readonly double[] _targetValues;
    private DateTime _animationStartUtc;

    private Task? _pollingTask;
    private Task? _animationTask;

    public ObservableCollection<MetricViewModel> Metrics { get; }

    [ObservableProperty]
    private string _gpuDisplayName = "GPU: поиск...";

    public StartupStatusViewModel StartupStatus { get; } = new();

    public MainViewModel(IHardwareMonitorService hardwareMonitorService, IStartupRegistrationService startupRegistrationService)
    {
        _hardwareMonitorService = hardwareMonitorService;
        _startupRegistrationService = startupRegistrationService;

        Metrics = new ObservableCollection<MetricViewModel>(
            MetricDefinition.All.Select(d => new MetricViewModel(d.Label, d.Unit)));

        _currentValues = new double[Metrics.Count];
        _startValues = new double[Metrics.Count];
        _targetValues = new double[Metrics.Count];
        _animationStartUtc = DateTime.UtcNow;
    }

    public async Task InitializeAsync()
    {
        await StartupStatus.RegisterAsync(_startupRegistrationService);
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
        var snapshot = await _hardwareMonitorService.ReadSnapshotAsync(cancellationToken);

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

        for (var i = 0; i < MetricDefinition.All.Length; i++)
        {
            var value = snapshot.Values.TryGetValue(MetricDefinition.All[i], out var v) ? v : 0d;
            SetNewTarget(i, value);
        }
    }

    private void SetNewTarget(int index, double newTarget)
    {
        _startValues[index] = _currentValues[index];
        _targetValues[index] = Math.Clamp(newTarget, 0, 100);
    }

    private void ApplyInterpolatedValues(DateTime nowUtc)
    {
        _animator.UpdateFrame(Metrics, _startValues, _currentValues, _targetValues, _animationStartUtc, nowUtc);
    }
}
