namespace HardwareMonitorWidget.Services.Hardware;

/// <summary>
/// Дополнительный интерфейс для ридера, который помимо числового значения
/// может вернуть отображаемое имя GPU. Реализуется GpuLoadReader.
/// </summary>
public interface IGpuNameReader
{
    string ReadGpuName(IHardwareContext context);
}
