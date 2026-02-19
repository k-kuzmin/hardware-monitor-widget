namespace HardwareMonitorWidget.Models;

/// <summary>
/// Дескриптор метрики. Является ключом в словаре HardwareSnapshot.Values.
/// Статические поля — singleton-экземпляры, reference equality гарантирована.
/// Добавление нового датчика: одна строка здесь + новый файл reader.
/// </summary>
public sealed class MetricDefinition
{
    public string Label { get; }
    public string Unit  { get; }

    private MetricDefinition(string label, string unit)
    {
        Label = label;
        Unit  = unit;
    }

    public static readonly MetricDefinition CpuLoad = new("CPU Load", "%");
    public static readonly MetricDefinition CpuTemp = new("CPU Temp", "°C");
    public static readonly MetricDefinition GpuLoad = new("GPU Load", "%");
    public static readonly MetricDefinition GpuTemp = new("GPU Temp", "°C");
    public static readonly MetricDefinition RamLoad = new("RAM Load", "%");

    /// <summary>Порядок определяет порядок отображения в виджете.</summary>
    public static readonly MetricDefinition[] All =
        [CpuLoad, CpuTemp, GpuLoad, GpuTemp, RamLoad];
}
