namespace HardwareMonitorWidget.Services.Hardware;

/// <summary>
/// Считывает одну числовую метрику из уже обновлённого IHardwareContext.
/// Каждый ридер самостоятельно описывает свою метрику: название, единицу измерения и логику чтения.
/// </summary>
public interface IMetricReader
{
    /// <summary>Отображаемое название метрики (например, "CPU Load").</summary>
    string Label { get; }

    /// <summary>Единица измерения (например, "%", "°C").</summary>
    string Unit { get; }

    double Read(IHardwareContext context);
}
