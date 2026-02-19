using LibreHardwareMonitor.Hardware;
using System.Management;
using HardwareMonitorWidget.Models;

namespace HardwareMonitorWidget.Services.Hardware.Readers;

/// <summary>
/// Считывает температуру CPU. Цепочка fallback:
/// 1. LHM: ядра/Package/Tctl/Tdie/CCD
/// 2. LHM: сенсоры материнской платы (ASUS, CPUTIN)
/// 3. LHM: любые температурные сенсоры CPU
/// 4. LHM: глобальный поиск по всем сенсорам
/// 5. Win32_PerfFormattedData_Counters_ThermalZoneInformation (без прав администратора)
/// </summary>
internal sealed class CpuTempReader : IMetricReader
{
    public MetricDefinition Definition => MetricDefinition.CpuTemp;

    public double Read(IHardwareContext context)
    {
        // 1. Ядра / Package / Tctl / Tdie / CCD
        var coreTemps = context.CpuSensors
            .Where(s => s.SensorType == SensorType.Temperature
                && (s.Name.Contains("Core",    StringComparison.OrdinalIgnoreCase)
                 || s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase)
                 || s.Name.Contains("Tctl",    StringComparison.OrdinalIgnoreCase)
                 || s.Name.Contains("Tdie",    StringComparison.OrdinalIgnoreCase)
                 || s.Name.Contains("CCD",     StringComparison.OrdinalIgnoreCase)))
            .Select(s => s.Value)
            .Where(v => v.HasValue)
            .Select(v => (double)v!.Value)
            .ToList();

        if (coreTemps.Count > 0) return SensorHelper.Clamp(coreTemps.Max());

        // 2. Материнская плата
        var mbTemp = SensorHelper.GetPreferred(context.MotherboardSensors, SensorType.Temperature,
            s => s.Name.Contains("CPU",       StringComparison.OrdinalIgnoreCase)
              || s.Name.Contains("Processor", StringComparison.OrdinalIgnoreCase)
              || s.Name.Contains("CPUTIN",    StringComparison.OrdinalIgnoreCase));
        if (mbTemp is > 0) return SensorHelper.Clamp(mbTemp.Value);

        // 3. Любые сенсоры CPU
        var anyTemp = SensorHelper.GetMax(context.CpuSensors, SensorType.Temperature);
        if (anyTemp is > 0) return SensorHelper.Clamp(anyTemp.Value);

        // 4. Глобальный поиск
        var globalTemp = SensorHelper.GetPreferred(context.AllSensors, SensorType.Temperature,
            s => s.Name.Contains("CPU",     StringComparison.OrdinalIgnoreCase)
              || s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase)
              || s.Name.Contains("Tctl",    StringComparison.OrdinalIgnoreCase)
              || s.Name.Contains("Tdie",    StringComparison.OrdinalIgnoreCase));
        if (globalTemp is > 0) return SensorHelper.Clamp(globalTemp.Value);

        // 5. Счётчики производительности Windows (без прав администратора)
        return SensorHelper.Clamp(TryReadPerfCounterThermalZone());
    }

    /// <summary>
    /// Win32_PerfFormattedData_Counters_ThermalZoneInformation — ACPI thermal zone.
    /// HighPrecisionTemperature в деси-Кельвинах (÷10 - 273.15).
    /// </summary>
    private static double TryReadPerfCounterThermalZone()
    {
        double maxTemp = 0;
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\cimv2",
                "SELECT HighPrecisionTemperature FROM Win32_PerfFormattedData_Counters_ThermalZoneInformation");

            foreach (var obj in searcher.Get())
            {
                var raw = obj["HighPrecisionTemperature"];
                if (raw is null) continue;
                var celsius = Convert.ToDouble(raw) / 10.0 - 273.15;
                if (celsius > 0 && celsius < 120)
                    maxTemp = Math.Max(maxTemp, celsius);
            }
        }
        catch { }

        return maxTemp;
    }
}
