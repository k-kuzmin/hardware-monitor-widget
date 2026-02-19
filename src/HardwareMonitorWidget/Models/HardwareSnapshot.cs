namespace HardwareMonitorWidget.Models;

/// <summary>
/// Снимок метрик за одно обновление железа.
/// Values индексирован singleton-ключами MetricDefinition — O(1) по reference equality.
/// </summary>
public sealed record HardwareSnapshot(
    IReadOnlyDictionary<MetricDefinition, double> Values,
    string GpuName
);
