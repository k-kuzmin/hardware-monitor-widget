using HardwareMonitorWidget.Models;
using LibreHardwareMonitor.Hardware;
using System.Management;
using System.Runtime.InteropServices;

namespace HardwareMonitorWidget.Services;

public sealed class LibreHardwareMonitorService : IHardwareMonitorService
{
    private Computer _computer;

    /// <summary>
    /// Периодическая реинициализация Computer предотвращает "залипание" сенсоров температуры,
    /// которое происходит когда LibreHardwareMonitor теряет связь с драйвером (после сна, гибернации и т.д.).
    /// </summary>
    private int _pollsSinceReinit;
    private const int ReinitEveryNPolls = 30; // каждые ~30 секунд


    public LibreHardwareMonitorService()
    {
        _computer = CreateComputer();
    }

    public Task<HardwareSnapshot> ReadSnapshotAsync(CancellationToken ct = default)
    {
        return Task.Run(ReadSnapshotCore, ct);
    }

    private HardwareSnapshot ReadSnapshotCore()
    {
        ReinitializeIfNeeded();

        UpdateAllHardware();

        var allHardware = _computer.Hardware.ToList();
        var sensorMap = BuildSensorMap(allHardware);
        var allSensors = sensorMap.Values.SelectMany(s => s).ToList();

        var cpu = allHardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        var motherboard = allHardware.FirstOrDefault(h => h.HardwareType == HardwareType.Motherboard);
        var gpuCandidates = allHardware.Where(IsGpu).ToList();
        var selectedGpu = SelectGpu(gpuCandidates, sensorMap);

        var cpuSensors = GetSensors(cpu, sensorMap);
        var motherboardSensors = GetSensors(motherboard, sensorMap);
        var gpuSensors = GetSensors(selectedGpu, sensorMap);

        return new HardwareSnapshot(
            CpuLoad: ClampToPercent(GetCpuLoad(cpuSensors)),
            CpuTemperature: ClampToPercent(GetCpuTemperature(cpuSensors, motherboardSensors, allSensors)),
            RamLoad: ClampToPercent(GetPhysicalMemoryLoad()),
            GpuLoad: ClampToPercent(GetGpuLoad(gpuSensors)),
            GpuTemperature: ClampToPercent(GetGpuTemperature(gpuSensors, allSensors)),
            GpuName: selectedGpu?.Name ?? "GPU не обнаружен");
    }

    public void Dispose()
    {
        _computer.Close();
    }

    private static Computer CreateComputer()
    {
        var computer = new Computer
        {
            IsCpuEnabled = true,
            IsMemoryEnabled = true,
            IsGpuEnabled = true,
            IsMotherboardEnabled = true
        };

        computer.Open();
        return computer;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    /// <summary>
    /// Читаем загрузку физической RAM напрямую через Win32 API — тот же источник, что и Диспетчер задач.
    /// </summary>
    private static double GetPhysicalMemoryLoad()
    {
        var memStatus = new MemoryStatusEx { dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        return GlobalMemoryStatusEx(ref memStatus) ? memStatus.dwMemoryLoad : 0;
    }

    private void ReinitializeIfNeeded()
    {
        _pollsSinceReinit++;
        if (_pollsSinceReinit < ReinitEveryNPolls)
        {
            return;
        }

        _pollsSinceReinit = 0;
        ReinitializeHardware();
    }

    private void ReinitializeHardware()
    {
        try { _computer.Close(); } catch { }
        _computer = CreateComputer();
    }

    private void UpdateAllHardware()
    {
        foreach (var hardware in _computer.Hardware)
            UpdateHardwareRecursive(hardware);
    }

    private static void UpdateHardwareRecursive(IHardware hardware)
    {
        hardware.Update();
        foreach (var sub in hardware.SubHardware)
            UpdateHardwareRecursive(sub);
    }

    private static bool IsGpu(IHardware hardware) =>
        hardware.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;

    private static bool IsDiscreteGpu(IHardware hardware) =>
        hardware.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd;

    private static IHardware? SelectGpu(IReadOnlyList<IHardware> gpus, IReadOnlyDictionary<IHardware, IReadOnlyList<ISensor>> sensorMap)
    {
        if (gpus.Count == 0) return null;

        var discrete = gpus.Where(IsDiscreteGpu).ToList();
        if (discrete.Count > 0)
        {
            var active = discrete
                .Select(g => new { Gpu = g, Load = GetGpuLoad(GetSensors(g, sensorMap)) })
                .OrderByDescending(x => x.Load)
                .FirstOrDefault(x => x.Load > 0.5)?.Gpu;

            if (active is not null) return active;
        }

        return gpus[0];
    }

    private static IEnumerable<ISensor> EnumerateSensors(IHardware hardware)
    {
        foreach (var sensor in hardware.Sensors) yield return sensor;
        foreach (var sub in hardware.SubHardware)
            foreach (var sensor in EnumerateSensors(sub))
                yield return sensor;
    }

    private static Dictionary<IHardware, IReadOnlyList<ISensor>> BuildSensorMap(IEnumerable<IHardware> hardwareCollection)
    {
        var map = new Dictionary<IHardware, IReadOnlyList<ISensor>>();
        foreach (var hw in hardwareCollection)
            map[hw] = EnumerateSensors(hw).ToList();
        return map;
    }

    private static IReadOnlyList<ISensor> GetSensors(IHardware? hardware, IReadOnlyDictionary<IHardware, IReadOnlyList<ISensor>> sensorMap)
    {
        if (hardware is null) return [];
        return sensorMap.TryGetValue(hardware, out var sensors) ? sensors : [];
    }

    private static double GetCpuLoad(IEnumerable<ISensor> cpuSensors) =>
        GetPreferredSensorValue(cpuSensors, SensorType.Load,
            s => s.Name.Contains("Total", StringComparison.OrdinalIgnoreCase))
        ?? GetMaxSensorValue(cpuSensors, SensorType.Load)
        ?? 0;

    /// <summary>
    /// Возвращает температуру CPU. Цепочка источников:
    /// 1. LHM: ядра/Package/Tctl/Tdie/CCD
    /// 2. LHM: сенсоры материнской платы (ASUS, CPUTIN)
    /// 3. LHM: любые температурные сенсоры CPU
    /// 4. LHM: глобальный поиск по всем сенсорам
    /// 5. Win32_PerfFormattedData_Counters_ThermalZoneInformation (без прав администратора)
    /// </summary>
    private double GetCpuTemperature(
        IEnumerable<ISensor> cpuSensors, IEnumerable<ISensor> motherboardSensors, IEnumerable<ISensor> allSensors)
    {
        // 1. Ядра / Package / Tctl / Tdie / CCD
        var coreTemps = cpuSensors
            .Where(s => s.SensorType == SensorType.Temperature
                && (s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)
                 || s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase)
                 || s.Name.Contains("Tctl", StringComparison.OrdinalIgnoreCase)
                 || s.Name.Contains("Tdie", StringComparison.OrdinalIgnoreCase)
                 || s.Name.Contains("CCD", StringComparison.OrdinalIgnoreCase)))
            .Select(s => s.Value)
            .Where(v => v.HasValue)
            .Select(v => (double)v!.Value)
            .ToList();

        if (coreTemps.Count > 0) return coreTemps.Max();

        // 2. Материнская плата
        var mbTemp = GetPreferredSensorValue(motherboardSensors, SensorType.Temperature,
            s => s.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase)
              || s.Name.Contains("Processor", StringComparison.OrdinalIgnoreCase)
              || s.Name.Contains("CPUTIN", StringComparison.OrdinalIgnoreCase));
        if (mbTemp is > 0) return mbTemp.Value;

        // 3. Любые сенсоры CPU
        var anyTemp = GetMaxSensorValue(cpuSensors, SensorType.Temperature);
        if (anyTemp is > 0) return anyTemp.Value;

        // 4. Глобальный поиск
        var globalTemp = GetPreferredSensorValue(allSensors, SensorType.Temperature,
            s => s.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase)
              || s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase)
              || s.Name.Contains("Tctl", StringComparison.OrdinalIgnoreCase)
              || s.Name.Contains("Tdie", StringComparison.OrdinalIgnoreCase));
        if (globalTemp is > 0) return globalTemp.Value;

        // 5. Счётчики производительности Windows (работают без прав администратора)
        return TryReadPerfCounterThermalZone();
    }

    /// <summary>
    /// Win32_PerfFormattedData_Counters_ThermalZoneInformation — ACPI thermal zone.
    /// Доступен без прав администратора. HighPrecisionTemperature в деси-Кельвинах.
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
        catch
        {
            // Недоступен на данной системе
        }

        return maxTemp;
    }

    private static double GetGpuLoad(IEnumerable<ISensor> gpuSensors) =>
        GetPreferredSensorValue(gpuSensors, SensorType.Load,
            s => s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)
              || s.Name.Contains("D3D", StringComparison.OrdinalIgnoreCase)
              || s.Name.Contains("GPU", StringComparison.OrdinalIgnoreCase))
        ?? GetMaxSensorValue(gpuSensors, SensorType.Load)
        ?? 0;

    private static double GetGpuTemperature(IEnumerable<ISensor> gpuSensors, IEnumerable<ISensor> allSensors)
    {
        var direct = GetPreferredSensorValue(gpuSensors, SensorType.Temperature,
                s => s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)
                  || s.Name.Contains("Hot Spot", StringComparison.OrdinalIgnoreCase)
                  || s.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase))
            ?? GetMaxSensorValue(gpuSensors, SensorType.Temperature)
            ?? 0;

        if (direct > 0) return direct;

        return GetPreferredSensorValue(allSensors, SensorType.Temperature,
            s => s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)
              || s.Name.Contains("Hot Spot", StringComparison.OrdinalIgnoreCase)
              || s.Name.Contains("GPU", StringComparison.OrdinalIgnoreCase))
            ?? GetMaxSensorValue(allSensors, SensorType.Temperature)
            ?? 0;
    }

    private static double? GetPreferredSensorValue(IEnumerable<ISensor> sensors, SensorType type, Func<ISensor, bool> predicate) =>
        sensors.Where(s => s.SensorType == type).Where(predicate)
               .Select(s => (double?)s.Value).FirstOrDefault(v => v.HasValue);

    private static double? GetMaxSensorValue(IEnumerable<ISensor> sensors, SensorType type) =>
        sensors.Where(s => s.SensorType == type)
               .Select(s => (double?)s.Value).Where(v => v.HasValue)
               .Select(v => v!.Value).DefaultIfEmpty().Max() is double d && d > 0 ? d : null;

    private static double ClampToPercent(double value) => Math.Clamp(value, 0, 100);
}
