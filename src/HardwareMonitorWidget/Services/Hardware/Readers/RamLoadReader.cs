using System.Runtime.InteropServices;

namespace HardwareMonitorWidget.Services.Hardware.Readers;

/// <summary>
/// Считывает загрузку RAM напрямую через Win32 GlobalMemoryStatusEx —
/// тот же источник, что и Диспетчер задач.
/// </summary>
internal sealed class RamLoadReader : IMetricReader
{
    public string Label => "RAM Load";
    public string Unit  => "%";

    public double Read(IHardwareContext context) =>
        SensorHelper.Clamp(GetPhysicalMemoryLoad());

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint  dwLength;
        public uint  dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    private static double GetPhysicalMemoryLoad()
    {
        var memStatus = new MemoryStatusEx { dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        return GlobalMemoryStatusEx(ref memStatus) ? memStatus.dwMemoryLoad : 0;
    }
}
