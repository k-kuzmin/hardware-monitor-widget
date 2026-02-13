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
        var cpu = allHardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        var memory = allHardware.FirstOrDefault(h => h.HardwareType == HardwareType.Memory);

        var gpuCandidates = allHardware
            .Where(IsGpu)
            .ToList();

        var selectedGpu = SelectGpu(gpuCandidates);

        var cpuLoad = GetCpuLoad(cpu);
        var cpuTemperature = GetCpuTemperature(cpu);
        var ramLoad = GetRamLoad(memory);
        var gpuLoad = GetGpuLoad(selectedGpu);
        var gpuTemperature = GetGpuTemperature(selectedGpu);

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

    private static IHardware? SelectGpu(IReadOnlyList<IHardware> gpus)
    {
        if (gpus.Count == 0)
        {
            return null;
        }

        var discrete = gpus.Where(IsDiscreteGpu).ToList();

        if (discrete.Count > 0)
        {
            var activeDiscrete = discrete
                .Select(gpu => new { Gpu = gpu, Load = GetGpuLoad(gpu) })
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

    private static double GetCpuLoad(IHardware? cpu)
    {
        if (cpu is null)
        {
            return 0;
        }

        return GetPreferredSensorValue(
            cpu,
            SensorType.Load,
            sensor => sensor.Name.Contains("Total", StringComparison.OrdinalIgnoreCase))
            ?? GetMaxSensorValue(cpu, SensorType.Load)
            ?? 0;
    }

    private static double GetCpuTemperature(IHardware? cpu)
    {
        if (cpu is null)
        {
            return 0;
        }

        return GetPreferredSensorValue(
            cpu,
            SensorType.Temperature,
            sensor => sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase))
            ?? GetMaxSensorValue(cpu, SensorType.Temperature)
            ?? 0;
    }

    private static double GetRamLoad(IHardware? memory)
    {
        if (memory is null)
        {
            return 0;
        }

        return GetPreferredSensorValue(
            memory,
            SensorType.Load,
            sensor => sensor.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase))
            ?? GetMaxSensorValue(memory, SensorType.Load)
            ?? 0;
    }

    private static double GetGpuLoad(IHardware? gpu)
    {
        if (gpu is null)
        {
            return 0;
        }

        return GetPreferredSensorValue(
            gpu,
            SensorType.Load,
            sensor => sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)
                   || sensor.Name.Contains("D3D", StringComparison.OrdinalIgnoreCase)
                   || sensor.Name.Contains("GPU", StringComparison.OrdinalIgnoreCase))
            ?? GetMaxSensorValue(gpu, SensorType.Load)
            ?? 0;
    }

    private static double GetGpuTemperature(IHardware? gpu)
    {
        if (gpu is null)
        {
            return 0;
        }

        return GetPreferredSensorValue(
            gpu,
            SensorType.Temperature,
            sensor => sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)
                   || sensor.Name.Contains("Hot Spot", StringComparison.OrdinalIgnoreCase))
            ?? GetMaxSensorValue(gpu, SensorType.Temperature)
            ?? 0;
    }

    private static double? GetPreferredSensorValue(IHardware hardware, SensorType sensorType, Func<ISensor, bool> predicate)
    {
        return EnumerateSensors(hardware)
            .Where(sensor => sensor.SensorType == sensorType)
            .Where(predicate)
            .Select(sensor => (double?)sensor.Value)
            .FirstOrDefault(value => value.HasValue);
    }

    private static double? GetMaxSensorValue(IHardware hardware, SensorType sensorType)
    {
        return EnumerateSensors(hardware)
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