using System.Runtime.InteropServices;
using System.Text.Json;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using HardwareMonitorWidget.Services;
using HardwareMonitorWidget.ViewModels;

namespace HardwareMonitorWidget;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private static readonly string PositionFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HardwareMonitorWidget",
        "window-position.json");

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const double SnapThreshold = 16;

    public MainWindow()
    {
        InitializeComponent();

        var hardwareMonitorService = new LibreHardwareMonitorService();
        var startupRegistrationService = new TaskSchedulerStartupRegistrationService();

        _viewModel = new MainViewModel(hardwareMonitorService, startupRegistrationService);
        DataContext = _viewModel;

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        TryRestoreWindowPosition();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Установка окна позади всех остальных окон
        var hwnd = new WindowInteropHelper(this).Handle;
        SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);

        await _viewModel.InitializeAsync();
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        SaveWindowPosition();

        SourceInitialized -= OnSourceInitialized;
        Loaded -= OnLoaded;
        Closed -= OnClosed;
        await _viewModel.DisposeAsync();
    }

    private void OnRootMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
            SnapToNearestMonitorEdges();
            SaveWindowPosition();
        }
    }

    private void SaveWindowPosition()
    {
        try
        {
            var directory = Path.GetDirectoryName(PositionFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var state = new WindowPositionState(Left, Top);
            var json = JsonSerializer.Serialize(state);
            File.WriteAllText(PositionFilePath, json);
        }
        catch
        {
        }
    }

    private void TryRestoreWindowPosition()
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

            WindowStartupLocation = WindowStartupLocation.Manual;

            var clamped = ClampToNearestMonitorWorkArea(state.Left, state.Top);
            Left = clamped.Left;
            Top = clamped.Top;
        }
        catch
        {
        }
    }

    private void SnapToNearestMonitorEdges()
    {
        var area = GetNearestMonitorWorkArea(Left, Top);
        var width = GetWindowWidthForBounds();
        var height = GetWindowHeightForBounds();

        var snappedLeft = Left;
        var snappedTop = Top;

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

        Left = ClampToRange(snappedLeft, area.Left, area.Right - width);
        Top = ClampToRange(snappedTop, area.Top, area.Bottom - height);
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
        var monitor = MonitorFromPoint(point, MONITOR_DEFAULTTONEAREST);

        var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref monitorInfo))
        {
            return PixelRectToDip(monitorInfo.rcWork);
        }

        var workArea = SystemParameters.WorkArea;
        return new Rect(workArea.Left, workArea.Top, workArea.Width, workArea.Height);
    }

    private POINT DipPointToPixel(double x, double y)
    {
        var transform = GetTransformToDevice();
        return new POINT(
            (int)Math.Round(x * transform.M11),
            (int)Math.Round(y * transform.M22));
    }

    private Rect PixelRectToDip(RECT rect)
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
        var source = PresentationSource.FromVisual(this);
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
        if (ActualWidth > 1)
        {
            return ActualWidth;
        }

        if (!double.IsNaN(Width) && Width > 1)
        {
            return Width;
        }

        return RestoreBounds.Width > 1 ? RestoreBounds.Width : 426;
    }

    private double GetWindowHeightForBounds()
    {
        if (ActualHeight > 1)
        {
            return ActualHeight;
        }

        if (!double.IsNaN(Height) && Height > 1)
        {
            return Height;
        }

        return RestoreBounds.Height > 1 ? RestoreBounds.Height : 260;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;

        public POINT(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private sealed record WindowPositionState(double Left, double Top);
}