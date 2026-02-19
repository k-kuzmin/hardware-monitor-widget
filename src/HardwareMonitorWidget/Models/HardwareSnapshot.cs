namespace HardwareMonitorWidget.Models;

/// <summary>
/// Снимок метрик за одно обновление железа.
/// Values[и] соответствует IMetricReader[и] в том же порядке, что Metrics[и] в ViewModel.
/// </summary>
public sealed record HardwareSnapshot(
    IReadOnlyList<double> Values,
    string GpuName
);
