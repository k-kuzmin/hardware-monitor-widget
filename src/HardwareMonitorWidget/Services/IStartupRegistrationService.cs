namespace HardwareMonitorWidget.Services;

public interface IStartupRegistrationService
{
    Task<bool> EnsureMachineWideAutostartAsync(string executablePath, CancellationToken cancellationToken = default);
}