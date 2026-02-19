using System.Diagnostics;
using System.IO;

namespace HardwareMonitorWidget.Services;

public sealed class TaskSchedulerStartupRegistrationService : IStartupRegistrationService
{
    private const string TaskName = "HardwareMonitorWidget";
    private static readonly TimeSpan SchTasksTimeout = TimeSpan.FromSeconds(10);

    public Task<bool> EnsureMachineWideAutostartAsync(string executablePath, CancellationToken cancellationToken = default)
    {
        return EnsureMachineWideAutostartAsyncCore(executablePath, cancellationToken);
    }

    private static async Task<bool> EnsureMachineWideAutostartAsyncCore(string executablePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath) || executablePath.Contains('"'))
        {
            return false;
        }

        var arguments = $"/Create /TN \"{TaskName}\" /TR \"\\\"{executablePath}\\\"\" /SC ONLOGON /RL HIGHEST /F";

        var startInfo = new ProcessStartInfo
        {
            FileName = "schtasks",
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return false;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(SchTasksTimeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
            return process.ExitCode == 0;
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            return false;
        }
    }
}