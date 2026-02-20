using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace HardwareMonitorWidget.Services;

public sealed class TaskSchedulerStartupRegistrationService : IStartupRegistrationService
{
    private const string TaskNamePrefix = "HardwareMonitorWidget";
    private const string LegacyTaskName = "HardwareMonitorWidget";
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

        var taskName = BuildTaskNameForCurrentUser();
        if (string.IsNullOrWhiteSpace(taskName))
        {
            return false;
        }

        var arguments = $"/Create /TN \"{taskName}\" /TR \"\\\"{executablePath}\\\"\" /SC ONLOGON /RL HIGHEST /F";

        var created = await RunSchTasksAsync(arguments, cancellationToken);
        if (!created)
        {
            return false;
        }

        if (!string.Equals(taskName, LegacyTaskName, StringComparison.OrdinalIgnoreCase))
        {
            await TryDeleteLegacyTaskAsync(cancellationToken);
        }

        return true;
    }

    private static string BuildTaskNameForCurrentUser()
    {
        var identity = WindowsIdentity.GetCurrent();
        var sid = identity.User?.Value;

        if (!string.IsNullOrWhiteSpace(sid))
        {
            return $"{TaskNamePrefix}-{sid}";
        }

        if (!string.IsNullOrWhiteSpace(identity.Name))
        {
            return $"{TaskNamePrefix}-{identity.Name.Replace('\\', '_')}";
        }

        return string.Empty;
    }

    private static async Task TryDeleteLegacyTaskAsync(CancellationToken cancellationToken)
    {
        var deleteArguments = $"/Delete /TN \"{LegacyTaskName}\" /F";
        await RunSchTasksAsync(deleteArguments, cancellationToken);
    }

    private static async Task<bool> RunSchTasksAsync(string arguments, CancellationToken cancellationToken)
    {
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