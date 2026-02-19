using LibreHardwareMonitor.Hardware;

namespace HardwareMonitorWidget.Services.Hardware;

/// <summary>
/// Снимок состояния железа после одного UpdateAllHardware().
/// Передаётся во все IMetricReader — обновление происходит один раз,
/// каждый ридер читает из уже обновлённого контекста.
/// </summary>
public interface IHardwareContext
{
    IReadOnlyList<ISensor> CpuSensors        { get; }
    IReadOnlyList<ISensor> MotherboardSensors { get; }
    IReadOnlyList<ISensor> GpuSensors        { get; }
    IReadOnlyList<ISensor> AllSensors        { get; }
    IHardware?             SelectedGpu       { get; }
}
