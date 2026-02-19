using HardwareMonitorWidget.Models;
using HardwareMonitorWidget.Services.Hardware;
using LibreHardwareMonitor.Hardware;

namespace HardwareMonitorWidget.Services;

/// <summary>
/// Тонкий оркестратор: обновляет железо один раз, строит IHardwareContext
/// и раздаёт его всем ридерам. Вся доменная логика — в ридерах.
/// </summary>
public sealed class LibreHardwareMonitorService : IHardwareMonitorService
{
    private readonly IReadOnlyList<IMetricReader> _readers;
    private readonly IGpuNameReader?              _gpuNameReader;

    private Computer _computer;

    /// <summary>
    /// Периодическая реинициализация предотвращает "залипание" сенсоров
    /// после сна/гибернации (потеря связи с драйвером LHM).
    /// </summary>
    private int _pollsSinceReinit;
    private const int ReinitEveryNPolls = 30; // каждые ~30 секунд

    public LibreHardwareMonitorService(IEnumerable<IMetricReader> readers)
    {
        _readers       = readers.ToList();
        _gpuNameReader = _readers.OfType<IGpuNameReader>().FirstOrDefault();
        _computer      = CreateComputer();
    }

    public Task<HardwareSnapshot> ReadSnapshotAsync(CancellationToken ct = default) =>
        Task.Run(ReadSnapshotCore, ct);

    private HardwareSnapshot ReadSnapshotCore()
    {
        ReinitializeIfNeeded();
        UpdateAllHardware();

        var context = new HardwareContext(_computer);

        var values = _readers.ToDictionary(
            r => r.Definition,
            r => r.Read(context));

        var gpuName = _gpuNameReader?.ReadGpuName(context) ?? "GPU не обнаружен";
        return new HardwareSnapshot(values, gpuName);
    }

    public void Dispose()
    {
        _computer.Close();
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private static Computer CreateComputer()
    {
        var computer = new Computer
        {
            IsCpuEnabled        = true,
            IsMemoryEnabled     = true,
            IsGpuEnabled        = true,
            IsMotherboardEnabled = true
        };
        computer.Open();
        return computer;
    }

    private void ReinitializeIfNeeded()
    {
        _pollsSinceReinit++;
        if (_pollsSinceReinit < ReinitEveryNPolls) return;
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
}