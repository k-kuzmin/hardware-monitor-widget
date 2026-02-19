using LibreHardwareMonitor.Hardware;

namespace HardwareMonitorWidget.Services.Hardware.Readers;

internal sealed class GpuTempReader : IMetricReader
{
    public string Label => "GPU Temp";
    public string Unit  => "°C";

    public double Read(IHardwareContext context)
    {
        var direct = SensorHelper.GetPreferred(context.GpuSensors, SensorType.Temperature,
                         s => s.Name.Contains("Core",     StringComparison.OrdinalIgnoreCase)
                           || s.Name.Contains("Hot Spot", StringComparison.OrdinalIgnoreCase)
                           || s.Name.Contains("Memory",   StringComparison.OrdinalIgnoreCase))
                     ?? SensorHelper.GetMax(context.GpuSensors, SensorType.Temperature)
                     ?? 0;

        if (direct > 0) return SensorHelper.Clamp(direct);

        // Fallback — глобальный поиск
        var global = SensorHelper.GetPreferred(context.AllSensors, SensorType.Temperature,
                         s => s.Name.Contains("Core",     StringComparison.OrdinalIgnoreCase)
                           || s.Name.Contains("Hot Spot", StringComparison.OrdinalIgnoreCase)
                           || s.Name.Contains("GPU",      StringComparison.OrdinalIgnoreCase))
                     ?? SensorHelper.GetMax(context.AllSensors, SensorType.Temperature)
                     ?? 0;

        return SensorHelper.Clamp(global);
    }
}
