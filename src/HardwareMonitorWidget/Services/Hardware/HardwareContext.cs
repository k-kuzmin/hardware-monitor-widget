using LibreHardwareMonitor.Hardware;

namespace HardwareMonitorWidget.Services.Hardware;

/// <summary>
/// Строится один раз после UpdateAllHardware(). Содержит предвычисленные
/// коллекции сенсоров — ридеры не обходят граф железа повторно.
/// </summary>
internal sealed class HardwareContext : IHardwareContext
{
    public IReadOnlyList<ISensor> CpuSensors        { get; }
    public IReadOnlyList<ISensor> MotherboardSensors { get; }
    public IReadOnlyList<ISensor> GpuSensors        { get; }
    // PERF-03: lazy — строится один раз при первом обращении (не на каждый poll)
    private readonly Lazy<IReadOnlyList<ISensor>> _allSensors;
    public IReadOnlyList<ISensor> AllSensors        => _allSensors.Value;
    public IHardware?             SelectedGpu       { get; }

    public HardwareContext(Computer computer)
    {
        var allHardware = computer.Hardware.ToList();
        var sensorMap   = BuildSensorMap(allHardware);

        var cpu         = allHardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        var motherboard = allHardware.FirstOrDefault(h => h.HardwareType == HardwareType.Motherboard);
        var gpuCandidates = allHardware.Where(IsGpu).ToList();

        SelectedGpu        = SelectGpu(gpuCandidates, sensorMap);
        CpuSensors         = GetSensors(cpu,         sensorMap);
        MotherboardSensors  = GetSensors(motherboard, sensorMap);
        GpuSensors         = GetSensors(SelectedGpu, sensorMap);
        _allSensors        = new Lazy<IReadOnlyList<ISensor>>(
            () => sensorMap.Values.SelectMany(s => s).ToList());
    }

    // ── GPU selection ────────────────────────────────────────────────────────

    private static bool IsGpu(IHardware h) =>
        h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;

    private static bool IsDiscreteGpu(IHardware h) =>
        h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd;

    private static IHardware? SelectGpu(
        IReadOnlyList<IHardware> gpus,
        IReadOnlyDictionary<IHardware, IReadOnlyList<ISensor>> sensorMap)
    {
        if (gpus.Count == 0) return null;

        var discrete = gpus.Where(IsDiscreteGpu).ToList();
        if (discrete.Count > 0)
        {
            var active = discrete
                .Select(g =>
                {
                    var sensors = GetSensors(g, sensorMap);
                    var load = SensorHelper.GetPreferredLoad(sensors,
                        s => s.Name.Contains("Core",  StringComparison.OrdinalIgnoreCase)
                          || s.Name.Contains("D3D",   StringComparison.OrdinalIgnoreCase)
                          || s.Name.Contains("GPU",   StringComparison.OrdinalIgnoreCase));
                    return (Gpu: g, Load: load);
                })
                .OrderByDescending(x => x.Load)
                .FirstOrDefault(x => x.Load > 0.5)
                .Gpu;

            if (active is not null) return active;
        }

        return gpus[0];
    }

    // ── Sensor map ───────────────────────────────────────────────────────────

    private static Dictionary<IHardware, IReadOnlyList<ISensor>> BuildSensorMap(
        IEnumerable<IHardware> hardware)
    {
        var map = new Dictionary<IHardware, IReadOnlyList<ISensor>>();
        foreach (var hw in hardware)
            map[hw] = EnumerateSensors(hw).ToList();
        return map;
    }

    private static IEnumerable<ISensor> EnumerateSensors(IHardware hardware)
    {
        foreach (var s in hardware.Sensors) yield return s;
        foreach (var sub in hardware.SubHardware)
            foreach (var s in EnumerateSensors(sub))
                yield return s;
    }

    private static IReadOnlyList<ISensor> GetSensors(
        IHardware? hardware,
        IReadOnlyDictionary<IHardware, IReadOnlyList<ISensor>> sensorMap)
    {
        if (hardware is null) return [];
        return sensorMap.TryGetValue(hardware, out var sensors) ? sensors : [];
    }
}
