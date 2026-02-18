namespace HardwareMonitorWidget.Models;

public sealed record HardwareSnapshot(
    double CpuLoad,
    double CpuTemperature,
    double RamLoad,
    double GpuLoad,
    double GpuTemperature,
    string GpuName
);
