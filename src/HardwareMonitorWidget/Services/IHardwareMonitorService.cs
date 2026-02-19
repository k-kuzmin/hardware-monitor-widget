using HardwareMonitorWidget.Models;

namespace HardwareMonitorWidget.Services;

public interface IHardwareMonitorService : IDisposable
{
    Task<HardwareSnapshot> ReadSnapshotAsync(CancellationToken ct = default);
}