using HardwareMonitorWidget.Models;

namespace HardwareMonitorWidget.Services;

public interface IHardwareMonitorService : IDisposable
{
    HardwareSnapshot ReadSnapshot();
}