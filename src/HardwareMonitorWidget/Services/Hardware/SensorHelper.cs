using LibreHardwareMonitor.Hardware;

namespace HardwareMonitorWidget.Services.Hardware;

/// <summary>
/// Утилитарные методы для работы с LibreHardwareMonitor-сенсорами.
/// Используются всеми IMetricReader для единообразной фильтрации.
/// </summary>
internal static class SensorHelper
{
    /// <summary>
    /// Возвращает значение первого подходящего по предикату сенсора заданного типа.
    /// </summary>
    public static double? GetPreferred(
        IEnumerable<ISensor> sensors,
        SensorType type,
        Func<ISensor, bool> predicate) =>
        sensors
            .Where(s => s.SensorType == type && predicate(s))
            .Select(s => (double?)s.Value)
            .FirstOrDefault(v => v.HasValue);

    /// <summary>
    /// Возвращает максимальное значение среди сенсоров заданного типа.
    /// </summary>
    public static double? GetMax(IEnumerable<ISensor> sensors, SensorType type)
    {
        var max = sensors
            .Where(s => s.SensorType == type && s.Value.HasValue)
            .Select(s => (double)s.Value!)
            .DefaultIfEmpty(double.NaN)
            .Max();

        return double.IsNaN(max) || max <= 0 ? null : max;
    }

    /// <summary>
    /// Ищет предпочтительное Load-значение с fallback на максимальный Load.
    /// </summary>
    public static double GetPreferredLoad(
        IEnumerable<ISensor> sensors,
        Func<ISensor, bool> predicate)
    {
        var list = sensors.ToList();
        return GetPreferred(list, SensorType.Load, predicate)
            ?? GetMax(list, SensorType.Load)
            ?? 0;
    }

    public static double Clamp(double value) => Math.Clamp(value, 0, 100);
}
