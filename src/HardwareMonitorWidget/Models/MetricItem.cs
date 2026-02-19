namespace HardwareMonitorWidget.Models;

/// <summary>
/// Чистая модель метрики без зависимости на UI-фреймворк.
/// Отображение управляется через MetricViewModel.
/// </summary>
public sealed record MetricItem(string Label, string Unit);