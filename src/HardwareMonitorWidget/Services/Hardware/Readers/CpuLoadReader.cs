using LibreHardwareMonitor.Hardware;
using HardwareMonitorWidget.Models;

namespace HardwareMonitorWidget.Services.Hardware.Readers;

internal sealed class CpuLoadReader : IMetricReader
{
    public MetricDefinition Definition => MetricDefinition.CpuLoad;

    public double Read(IHardwareContext context)
    {
        var load = SensorHelper.GetPreferred(context.CpuSensors, SensorType.Load,
                       s => s.Name.Contains("Total", StringComparison.OrdinalIgnoreCase))
                   ?? SensorHelper.GetMax(context.CpuSensors, SensorType.Load)
                   ?? 0;

        return SensorHelper.Clamp(load);
    }
}
