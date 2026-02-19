using LibreHardwareMonitor.Hardware;

namespace HardwareMonitorWidget.Services.Hardware.Readers;

/// <summary>
/// Считывает загрузку GPU и его отображаемое имя (IGpuNameReader).
/// </summary>
internal sealed class GpuLoadReader : IMetricReader, IGpuNameReader
{
    public string Label => "GPU Load";
    public string Unit  => "%";

    public double Read(IHardwareContext context)
    {
        var load = SensorHelper.GetPreferred(context.GpuSensors, SensorType.Load,
                       s => s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)
                         || s.Name.Contains("D3D",  StringComparison.OrdinalIgnoreCase)
                         || s.Name.Contains("GPU",  StringComparison.OrdinalIgnoreCase))
                   ?? SensorHelper.GetMax(context.GpuSensors, SensorType.Load)
                   ?? 0;

        return SensorHelper.Clamp(load);
    }

    public string ReadGpuName(IHardwareContext context) =>
        context.SelectedGpu?.Name ?? "GPU не обнаружен";
}
