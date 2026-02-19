using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using HardwareMonitorWidget.Infrastructure.Win32;

namespace HardwareMonitorWidget.Infrastructure;

internal sealed class WindowPositionService
{
    private static readonly string PositionFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HardwareMonitorWidget",
        "window-position.json");

    private const double SnapThreshold = 16;

    private readonly Window _window;

    public WindowPositionService(Window window)
    {
        _window = window;
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(PositionFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var state = new WindowPositionState(_window.Left, _window.Top);
            var json = JsonSerializer.Serialize(state);
            File.WriteAllText(PositionFilePath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HardwareMonitor] Не удалось сохранить позицию окна: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public void TryRestore()
    {
        try
        {
            if (!File.Exists(PositionFilePath))
            {
                return;
            }

            var json = File.ReadAllText(PositionFilePath);
            var state = JsonSerializer.Deserialize<WindowPositionState>(json);
            if (state is null)
            {
                return;
            }

            _window.WindowStartupLocation = WindowStartupLocation.Manual;

            var clamped = ClampToNearestMonitorWorkArea(state.Left, state.Top);
            _window.Left = clamped.Left;
            _window.Top = clamped.Top;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HardwareMonitor] Не удалось восстановить позицию окна: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public void SnapToEdgesAndSave()
    {
        SnapToNearestMonitorEdges();
        Save();
    }

    private void SnapToNearestMonitorEdges()
    {
        var area = GetNearestMonitorWorkArea(_window.Left, _window.Top);
        var width = GetWindowWidthForBounds();
        var height = GetWindowHeightForBounds();

        var snappedLeft = _window.Left;
        var snappedTop = _window.Top;

        if (Math.Abs(snappedLeft - area.Left) <= SnapThreshold)
        {
            snappedLeft = area.Left;
        }
        else if (Math.Abs((snappedLeft + width) - area.Right) <= SnapThreshold)
        {
            snappedLeft = area.Right - width;
        }

        if (Math.Abs(snappedTop - area.Top) <= SnapThreshold)
        {
            snappedTop = area.Top;
        }
        else if (Math.Abs((snappedTop + height) - area.Bottom) <= SnapThreshold)
        {
            snappedTop = area.Bottom - height;
        }

        _window.Left = ClampToRange(snappedLeft, area.Left, area.Right - width);
        _window.Top = ClampToRange(snappedTop, area.Top, area.Bottom - height);
    }

    private (double Left, double Top) ClampToNearestMonitorWorkArea(double left, double top)
    {
        var area = GetNearestMonitorWorkArea(left, top);
        var width = GetWindowWidthForBounds();
        var height = GetWindowHeightForBounds();

        var clampedLeft = ClampToRange(left, area.Left, area.Right - width);
        var clampedTop = ClampToRange(top, area.Top, area.Bottom - height);

        return (clampedLeft, clampedTop);
    }

    private Rect GetNearestMonitorWorkArea(double left, double top)
    {
        var point = DipPointToPixel(left, top);
        var monitor = Win32Api.MonitorFromPoint(point, Win32Api.MONITOR_DEFAULTTONEAREST);

        var monitorInfo = new Win32Api.MONITORINFO { cbSize = Marshal.SizeOf<Win32Api.MONITORINFO>() };
        if (monitor != IntPtr.Zero && Win32Api.GetMonitorInfo(monitor, ref monitorInfo))
        {
            return PixelRectToDip(monitorInfo.rcWork);
        }

        var workArea = SystemParameters.WorkArea;
        return new Rect(workArea.Left, workArea.Top, workArea.Width, workArea.Height);
    }

    private Win32Api.POINT DipPointToPixel(double x, double y)
    {
        var transform = GetTransformToDevice();
        return new Win32Api.POINT(
            (int)Math.Round(x * transform.M11),
            (int)Math.Round(y * transform.M22));
    }

    private Rect PixelRectToDip(Win32Api.RECT rect)
    {
        var transform = GetTransformToDevice();
        var scaleX = transform.M11 == 0 ? 1 : transform.M11;
        var scaleY = transform.M22 == 0 ? 1 : transform.M22;

        return new Rect(
            rect.Left / scaleX,
            rect.Top / scaleY,
            (rect.Right - rect.Left) / scaleX,
            (rect.Bottom - rect.Top) / scaleY);
    }

    private Matrix GetTransformToDevice()
    {
        var source = PresentationSource.FromVisual(_window);
        return source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
    }

    private static double ClampToRange(double value, double min, double max)
    {
        if (max < min)
        {
            return min;
        }

        return Math.Clamp(value, min, max);
    }

    private double GetWindowWidthForBounds()
    {
        if (_window.ActualWidth > 1)
        {
            return _window.ActualWidth;
        }

        if (!double.IsNaN(_window.Width) && _window.Width > 1)
        {
            return _window.Width;
        }

        return _window.RestoreBounds.Width > 1 ? _window.RestoreBounds.Width : 426;
    }

    private double GetWindowHeightForBounds()
    {
        if (_window.ActualHeight > 1)
        {
            return _window.ActualHeight;
        }

        if (!double.IsNaN(_window.Height) && _window.Height > 1)
        {
            return _window.Height;
        }

        return _window.RestoreBounds.Height > 1 ? _window.RestoreBounds.Height : 260;
    }

    private sealed record WindowPositionState(double Left, double Top);
}
