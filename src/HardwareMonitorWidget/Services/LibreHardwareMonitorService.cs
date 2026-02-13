using HardwareMonitorWidget.Models;
using LibreHardwareMonitor.Hardware;

namespace HardwareMonitorWidget.Services;

public sealed class LibreHardwareMonitorService : IHardwareMonitorService
{
    private readonly Computer _computer;

    public LibreHardwareMonitorService()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsMemoryEnabled = true,
            IsGpuEnabled = true
        };

        _computer.Open();
    }

    public HardwareSnapshot ReadSnapshot()
    {
        UpdateAllHardware();

        var allHardware = _computer.Hardware.ToList();
        var sensorMap = BuildSensorMap(allHardware);
        var allSensors = sensorMap.Values.SelectMany(sensors => sensors).ToList();

        var cpu = allHardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        var memory = allHardware.FirstOrDefault(h => h.HardwareType == HardwareType.Memory);

        var gpuCandidates = allHardware
            .Where(IsGpu)
            .ToList();

        var selectedGpu = SelectGpu(gpuCandidates, sensorMap);

        var cpuSensors = GetSensors(cpu, sensorMap);
        var memorySensors = GetSensors(memory, sensorMap);
        var gpuSensors = GetSensors(selectedGpu, sensorMap);

        var cpuLoad = GetCpuLoad(cpuSensors);
        var cpuTemperature = GetCpuTemperature(cpuSensors, allSensors);
        var ramLoad = GetRamLoad(memorySensors);
        var gpuLoad = GetGpuLoad(gpuSensors);
        var gpuTemperature = GetGpuTemperature(gpuSensors, allSensors);

        return new HardwareSnapshot(
            CpuLoad: ClampToPercent(cpuLoad),
            CpuTemperature: ClampToPercent(cpuTemperature),
            RamLoad: ClampToPercent(ramLoad),
            GpuLoad: ClampToPercent(gpuLoad),
            GpuTemperature: ClampToPercent(gpuTemperature),
            GpuName: selectedGpu?.Name ?? "GPU не обнаружен");
    }

    public void Dispose()
    {
        _computer.Close();
    }

    private void UpdateAllHardware()
    {
        foreach (var hardware in _computer.Hardware)
        {
            UpdateHardwareRecursive(hardware);
        }
    }

    private static void UpdateHardwareRecursive(IHardware hardware)
    {
        hardware.Update();

        foreach (var subHardware in hardware.SubHardware)
        {
            UpdateHardwareRecursive(subHardware);
        }
    }

    private static bool IsGpu(IHardware hardware)
    {
        return hardware.HardwareType is HardwareType.GpuNvidia
            or HardwareType.GpuAmd
            or HardwareType.GpuIntel;
    }

    private static bool IsDiscreteGpu(IHardware hardware)
    {
        return hardware.HardwareType is HardwareType.GpuNvidia
            or HardwareType.GpuAmd;
    }

    private static IHardware? SelectGpu(IReadOnlyList<IHardware> gpus, IReadOnlyDictionary<IHardware, IReadOnlyList<ISensor>> sensorMap)
    {
        if (gpus.Count == 0)
        {
            return null;
        }

        var discrete = gpus.Where(IsDiscreteGpu).ToList();

        if (discrete.Count > 0)
        {
            var activeDiscrete = discrete
                .Select(gpu => new { Gpu = gpu, Load = GetGpuLoad(GetSensors(gpu, sensorMap)) })
                .OrderByDescending(item => item.Load)
                .FirstOrDefault(item => item.Load > 0.5)?.Gpu;

            if (activeDiscrete is not null)
            {
                return activeDiscrete;
            }
        }

        return gpus[0];
    }

    private static IEnumerable<ISensor> EnumerateSensors(IHardware hardware)
    {
        foreach (var sensor in hardware.Sensors)
        {
            yield return sensor;
        }

        foreach (var subHardware in hardware.SubHardware)
        {
            foreach (var sensor in EnumerateSensors(subHardware))
            {
                yield return sensor;
            }
        }
    }

    private static Dictionary<IHardware, IReadOnlyList<ISensor>> BuildSensorMap(IEnumerable<IHardware> hardwareCollection)
    {
        var sensorMap = new Dictionary<IHardware, IReadOnlyList<ISensor>>();

        foreach (var hardware in hardwareCollection)
        {
            sensorMap[hardware] = EnumerateSensors(hardware).ToList();
        }

        return sensorMap;
    }

    private static IReadOnlyList<ISensor> GetSensors(IHardware? hardware, IReadOnlyDictionary<IHardware, IReadOnlyList<ISensor>> sensorMap)
    {
        if (hardware is null)
        {
            return [];
        }

        return sensorMap.TryGetValue(hardware, out var sensors) ? sensors : [];
    }

    private static double GetCpuLoad(IEnumerable<ISensor> cpuSensors)
    {
        return GetPreferredSensorValue(
            cpuSensors,
            SensorType.Load,
            sensor => sensor.Name.Contains("Total", StringComparison.OrdinalIgnoreCase))
            ?? GetMaxSensorValue(cpuSensors, SensorType.Load)
            ?? 0;
    }

    private static double GetCpuTemperature(IEnumerable<ISensor> cpuSensors, IEnumerable<ISensor> allSensors)
    {
        var directCpuTemperature = GetPreferredSensorValue(
                cpuSensors,
                SensorType.Temperature,
                sensor => sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase)
                       || sensor.Name.Contains("Tctl", StringComparison.OrdinalIgnoreCase)
                       || sensor.Name.Contains("Tdie", StringComparison.OrdinalIgnoreCase)
                       || sensor.Name.Contains("Core Max", StringComparison.OrdinalIgnoreCase))
            ?? GetMaxSensorValue(cpuSensors, SensorType.Temperature)
            ?? 0;

        if (directCpuTemperature > 0)
        {
            return directCpuTemperature;
        }

        return GetPreferredSensorValue(
            allSensors,
            SensorType.Temperature,
            sensor => sensor.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase)
                   || sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase)
                   || sensor.Name.Contains("Tctl", StringComparison.OrdinalIgnoreCase)
                   || sensor.Name.Contains("Tdie", StringComparison.OrdinalIgnoreCase))
            ?? GetMaxSensorValue(allSensors, SensorType.Temperature)
            ?? 0;
    }

    private static double GetRamLoad(IEnumerable<ISensor> memorySensors)
    {
        return GetPreferredSensorValue(
            memorySensors,
            SensorType.Load,
            sensor => sensor.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase))
            ?? GetMaxSensorValue(memorySensors, SensorType.Load)
            ?? 0;
    }

    private static double GetGpuLoad(IEnumerable<ISensor> gpuSensors)
    {
        return GetPreferredSensorValue(
            gpuSensors,
            SensorType.Load,
            sensor => sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)
                   || sensor.Name.Contains("D3D", StringComparison.OrdinalIgnoreCase)
                   || sensor.Name.Contains("GPU", StringComparison.OrdinalIgnoreCase))
            ?? GetMaxSensorValue(gpuSensors, SensorType.Load)
            ?? 0;
    }

    private static double GetGpuTemperature(IEnumerable<ISensor> gpuSensors, IEnumerable<ISensor> allSensors)
    {
        var directGpuTemperature = GetPreferredSensorValue(
                gpuSensors,
                SensorType.Temperature,
                sensor => sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)
                       || sensor.Name.Contains("Hot Spot", StringComparison.OrdinalIgnoreCase)
                       || sensor.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase))
            ?? GetMaxSensorValue(gpuSensors, SensorType.Temperature)
            ?? 0;

        if (directGpuTemperature > 0)
        {
            return directGpuTemperature;
        }

        return GetPreferredSensorValue(
            allSensors,
            SensorType.Temperature,
            sensor => sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)
                   || sensor.Name.Contains("Hot Spot", StringComparison.OrdinalIgnoreCase)
                   || sensor.Name.Contains("GPU", StringComparison.OrdinalIgnoreCase))
            ?? GetMaxSensorValue(allSensors, SensorType.Temperature)
            ?? 0;
    }

    private static double? GetPreferredSensorValue(IEnumerable<ISensor> sensors, SensorType sensorType, Func<ISensor, bool> predicate)
    {
        return sensors
            .Where(sensor => sensor.SensorType == sensorType)
            .Where(predicate)
            .Select(sensor => (double?)sensor.Value)
            .FirstOrDefault(value => value.HasValue);
    }

    private static double? GetMaxSensorValue(IEnumerable<ISensor> sensors, SensorType sensorType)
    {
        return sensors
            .Where(sensor => sensor.SensorType == sensorType)
            .Select(sensor => (double?)sensor.Value)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .DefaultIfEmpty()
            .Max();
    }

    private static double ClampToPercent(double value)
    {
        return Math.Clamp(value, 0, 100);
    }
}