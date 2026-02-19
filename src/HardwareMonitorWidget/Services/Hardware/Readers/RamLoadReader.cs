using System.Runtime.InteropServices;
using HardwareMonitorWidget.Infrastructure.Win32;

namespace HardwareMonitorWidget.Services.Hardware.Readers;

/// <summary>
/// Считывает загрузку RAM напрямую через Win32 GlobalMemoryStatusEx —
/// тот же источник, что и Диспетчер задач.
/// </summary>
internal sealed class RamLoadReader : IMetricReader
{
    public string Label => "Загр. ОЗУ";
    public string Unit => "%";

    public double Read(IHardwareContext context) =>
        SensorHelper.Clamp(GetPhysicalMemoryLoad());

    private static double GetPhysicalMemoryLoad()
    {
        var memStatus = new Win32Api.MemoryStatusEx { dwLength = (uint)Marshal.SizeOf<Win32Api.MemoryStatusEx>() };
        return Win32Api.GlobalMemoryStatusEx(ref memStatus) ? memStatus.dwMemoryLoad : 0;
    }
}
