namespace HardwareMonitorWidget.Models;

public enum MetricId
{
    CpuLoad,
    CpuTemp,
    GpuLoad,
    GpuTemp,
    RamLoad
}

public sealed record MetricDefinition(
    MetricId Id,
    string Label,
    string Unit,
    Func<HardwareSnapshot, double> Selector)
{
    public static readonly MetricDefinition[] All =
    [
        new(MetricId.CpuLoad, "CPU Load", "%",  s => s.CpuLoad),
        new(MetricId.CpuTemp, "CPU Temp", "°C", s => s.CpuTemperature),
        new(MetricId.GpuLoad, "GPU Load", "%",  s => s.GpuLoad),
        new(MetricId.GpuTemp, "GPU Temp", "°C", s => s.GpuTemperature),
        new(MetricId.RamLoad, "RAM Load", "%",  s => s.RamLoad),
    ];
}
