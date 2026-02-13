using System.Diagnostics;
using System.IO;

namespace HardwareMonitorWidget.Services;

public sealed class TaskSchedulerStartupRegistrationService : IStartupRegistrationService
{
    private const string TaskName = "HardwareMonitorWidget";

    public Task<bool> EnsureMachineWideAutostartAsync(string executablePath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => EnsureMachineWideAutostart(executablePath), cancellationToken);
    }

    private static bool EnsureMachineWideAutostart(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return false;
        }

        var arguments = $"/Create /TN \"{TaskName}\" /TR \"\\\"{executablePath}\\\"\" /SC ONLOGON /RL HIGHEST /RU Users /F";

        var startInfo = new ProcessStartInfo
        {
            FileName = "schtasks",
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return false;
        }

        process.WaitForExit();
        return process.ExitCode == 0;
    }
}