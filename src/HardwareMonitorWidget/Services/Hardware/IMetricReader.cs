using HardwareMonitorWidget.Models;

namespace HardwareMonitorWidget.Services.Hardware;

/// <summary>
/// Считывает одну числовую метрику из уже обновлённого IHardwareContext.
/// </summary>
public interface IMetricReader
{
    MetricDefinition Definition { get; }
    double Read(IHardwareContext context);
}
