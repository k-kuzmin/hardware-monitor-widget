# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

WPF (.NET 8) desktop widget for real-time CPU/GPU/RAM monitoring with cyberpunk aesthetics. Uses LibreHardwareMonitor for hardware metrics and CommunityToolkit.Mvvm for MVVM architecture.

## Language Requirement

**All UI text, comments, messages, and metric names must be in Russian.** This is a critical project convention.

## Build & Development Commands

```powershell
# Build
dotnet build HardwareMonitorWidget.sln

# Run
dotnet run --project src/HardwareMonitorWidget/HardwareMonitorWidget.csproj

# Release build
dotnet build HardwareMonitorWidget.sln -c Release -v minimal

# Publish (framework-dependent, win-x64)
dotnet publish src/HardwareMonitorWidget/HardwareMonitorWidget.csproj -c Release -r win-x64 --self-contained false -o artifacts/release/win-x64
```

## Architecture

### Dual-Loop Performance Model

The application uses two independent async loops for optimal performance:

1. **Polling loop** (1 second interval): Reads `HardwareSnapshot` via `LibreHardwareMonitorService`
2. **Animation loop** (50ms/~20 FPS): Interpolates metric values for smooth animations (700ms duration)

This separation is critical: hardware polling is expensive but infrequent; UI updates are lightweight but frequent.

### MVVM Structure

- **MainWindow.xaml/.cs**: WPF window with Win32 interop for positioning/monitor handling
- **MainViewModel**: Owns service lifecycle, coordinates dual loops, manages `ObservableCollection<MetricItem>`
- **Services**:
  - `IHardwareMonitorService` → `LibreHardwareMonitorService`: Hardware abstraction layer
  - `IStartupRegistrationService` → `TaskSchedulerStartupRegistrationService`: Autostart management via Task Scheduler
- **Models**:
  - `HardwareSnapshot`: Immutable snapshot of CPU/GPU/RAM metrics
  - `MetricItem`: Observable metric with animated bars and color-coded text

### Performance Optimizations

1. **Brush Palette Caching**: `BarBrushPalette[101]` and `TextBrushPalette[101]` are pre-created in `MainViewModel` static constructor to eliminate allocations in the UI loop. All brushes must be `.Freeze()` before palette insertion.

2. **ReferenceEquals Check**: Before updating `MetricItem.BarBrush`/`TextBrush`, reference equality is checked to avoid unnecessary property change notifications.

## CPU Temperature Fallback Chain

`LibreHardwareMonitorService.ReadSnapshot()` implements a 5-priority fallback for CPU temperature:

1. Core/Package/Tctl/Tdie/CCD sensors from CPU hardware
2. Motherboard sensors (ASUS/CPUTIN)
3. Any CPU temperature sensors
4. Global search across all sensors
5. `Win32_PerfFormattedData_Counters_ThermalZoneInformation` (`root\cimv2`) — **works without admin rights**; field `HighPrecisionTemperature` in deci-Kelvin (÷10 - 273.15).

> Note: `MSAcpi_ThermalZoneTemperature` (`root\WMI`) was removed — it requires Administrator and is blocked by HVCI on this system.

When debugging sensor issues, check `BuildSensorMap()` and sensor type matching logic in [LibreHardwareMonitorService.cs](src/HardwareMonitorWidget/Services/LibreHardwareMonitorService.cs).

## GPU Selection Logic

`SelectGpu()` in `LibreHardwareMonitorService`:

1. If discrete GPU (NVIDIA/AMD) exists with load > 0.5%, selects the most loaded one
2. Otherwise returns first available GPU
3. Integrated Intel GPUs have lowest priority

## Window Behavior

- **Always Behind**: `SetWindowPos(hwnd, HWND_BOTTOM, ...)` in `OnLoaded` keeps window behind all others
- **Position Persistence**: Saved to `%LocalAppData%\HardwareMonitorWidget\window-position.json`
- **Multi-Monitor Clamping**: `ClampToNearestMonitorWorkArea()` uses Win32 `MonitorFromPoint`/`GetMonitorInfo` for correct position restoration when monitors are disconnected
- **Snap Threshold**: 16 DIP for edge snapping during drag
- **DPI Awareness**: Uses `VisualTreeHelper.GetTransform()` for DIP ↔ Physical Pixels conversion

## Visual Design Patterns

- **Progressive Gradient System**: Bars change gradient as values increase (green → yellow → red)
- **Integer Display Only**: `MetricItem.Value` is bound with `StringFormat={}{0:0}` in XAML (no decimal part)
- **Startup Status Display**: Orange warning badge (⚠ `#FF9A2F`, border `#B35C00`, bg `#120900`) — only visible on autostart errors via `StartupStatusVisible` (Visibility.Collapsed on success); ToolTip shows details

## Autostart Implementation

`TaskSchedulerStartupRegistrationService` uses `schtasks` CLI (not registry):

```powershell
schtasks /Create /TN "HardwareMonitorWidget" /TR "\"path\to\exe\"" /SC ONLOGON /RL HIGHEST /F
```

- Timeout: 10 seconds
- Privilege level: `HIGHEST` (`/RL HIGHEST` — required for LibreHardwareMonitor kernel driver)
- Trigger: `ONLOGON` for current user
- **Manifest**: `requireAdministrator` in app.manifest for hardware sensor access
- On error/failure: orange ⚠ badge shown in widget header (Visibility.Visible); hidden on success (Visibility.Collapsed)

## LibreHardwareMonitor Integration

- Initialization: `Computer` requires `IsCpuEnabled`, `IsMemoryEnabled`, `IsGpuEnabled`, `IsMotherboardEnabled` set to true
- **Critical**: Always call `UpdateHardwareRecursive()` before reading sensors
- Sensor filtering: `SensorType.Load` for percentages, `SensorType.Temperature` for temperatures
- Hardware polling must always run in `Task.Run()` to avoid blocking UI thread (see `RefreshTargetsAsync()`)

## Dependencies

```xml
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
<PackageReference Include="LibreHardwareMonitorLib" Version="0.9.5" />
<PackageReference Include="System.Management" Version="10.0.1" />
```

All dependencies are stable; avoid breaking changes when updating.

## Common Pitfalls

- **Temperature units**: LibreHardwareMonitor returns °C; `Win32_PerfFormattedData_Counters_ThermalZoneInformation.HighPrecisionTemperature` returns deci-Kelvin (÷10 - 273.15)
- **JSON serialization**: `WindowPositionState` record requires simple types (double) for `System.Text.Json`
- **Brush freezing**: All brush objects must be `.Freeze()` before palette use
- **UI thread blocking**: Hardware polling always through `Task.Run()` in `RefreshTargetsAsync()`
