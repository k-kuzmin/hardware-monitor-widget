using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace HardwareMonitorWidget.Services;

public sealed class TaskSchedulerStartupRegistrationService : IStartupRegistrationService
{
    private const string TaskNamePrefix = "HardwareMonitorWidget";
    private static readonly TimeSpan SchTasksTimeout = TimeSpan.FromSeconds(10);
    private static readonly Lazy<string> CurrentUserSid = new(GetCurrentUserSid);
    private static readonly SemaphoreSlim EnsureLock = new(1, 1);
    private static string? _lastExecutablePath;
    private static bool _lastEnsureSucceeded;

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

        var fullExecutablePath = Path.GetFullPath(executablePath);

        await EnsureLock.WaitAsync(cancellationToken);
        try
        {
            if (_lastEnsureSucceeded && string.Equals(_lastExecutablePath, fullExecutablePath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var taskName = BuildTaskNameForCurrentUser();
            if (string.IsNullOrWhiteSpace(taskName))
            {
                return false;
            }

            var created = await CreateOrUpdateStartupTaskAsync(taskName, fullExecutablePath, cancellationToken);
            if (created)
            {
                _lastExecutablePath = fullExecutablePath;
                _lastEnsureSucceeded = true;
                return true;
            }

            _lastEnsureSucceeded = false;
            return false;
        }
        finally
        {
            EnsureLock.Release();
        }
    }

    private static string BuildTaskNameForCurrentUser()
    {
        var sid = CurrentUserSid.Value;

        if (!string.IsNullOrWhiteSpace(sid))
        {
            return $"{TaskNamePrefix}-{sid}";
        }

        return string.Empty;
    }

    private static string GetCurrentUserSid()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return identity.User?.Value ?? string.Empty;
    }

    private static Task<bool> CreateOrUpdateStartupTaskAsync(string taskName, string executablePath, CancellationToken cancellationToken)
    {
        var taskRunCommand = $"\"{executablePath}\"";
        return RunSchTasksAsync(
            [
                "/Create",
                "/TN",
                taskName,
                "/TR",
                taskRunCommand,
                "/SC",
                "ONLOGON",
                "/RL",
                "HIGHEST",
                "/F"
            ],
            cancellationToken);
    }

    private static async Task<bool> RunSchTasksAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "schtasks",
            CreateNoWindow = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

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