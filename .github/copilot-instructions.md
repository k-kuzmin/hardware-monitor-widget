# Hardware Monitor Widget - AI Coding Agent Instructions

## Project Overview

Компактный Windows desktop виджет для мониторинга CPU/GPU/RAM в реальном времени с cyberpunk-эстетикой. WPF (.NET 8) приложение использующее LibreHardwareMonitor для сбора метрик и CommunityToolkit.Mvvm для MVVM.

## Architecture & Key Design Decisions

### Dual-Loop Architecture
Производительность обеспечивается **двумя независимыми async циклами**:
- **Polling loop** (1 секунда): читает `HardwareSnapshot` через `LibreHardwareMonitorService`
- **Animation loop** (50ms/~20 FPS): интерполирует значения метрик для плавной анимации (700ms duration)

Это разделение критично: hardware polling тяжёлый, но редкий; UI updates лёгкие, но частые.

### Performance Optimizations
1. **Brush Palette Caching**: `BarBrushPalette[101]` и `TextBrushPalette[101]` предварительно созданы в `MainViewModel` статическом конструкторе для устранения аллокаций в UI-цикле
2. **WMI Caching**: `LibreHardwareMonitorService` кэширует WMI CPU temperature запросы на 3 секунды для снижения накладных расходов
3. **ReferenceEquals Check**: перед обновлением `MetricItem.BarBrush`/`TextBrush` проверяется идентичность ссылок

### Service Architecture
MVVM с dependency injection через конструктор:
- `IHardwareMonitorService` → `LibreHardwareMonitorService`: hardware abstraction
- `IStartupRegistrationService` → `TaskSchedulerStartupRegistrationService`: autostart management

`MainViewModel` владеет обоими сервисами и координирует их жизненный цикл.

## Critical Developer Workflows

### Build & Run
```powershell
# Development build and run
dotnet build HardwareMonitorWidget.sln
dotnet run --project src/HardwareMonitorWidget/HardwareMonitorWidget.csproj

# Release build
dotnet build HardwareMonitorWidget.sln -c Release -v minimal

# Framework-dependent publish (win-x64)
dotnet publish src/HardwareMonitorWidget/HardwareMonitorWidget.csproj -c Release -r win-x64 --self-contained false -o artifacts/release/win-x64
```

### Debugging Hardware Sensors
`LibreHardwareMonitorService.ReadSnapshot()` имеет сложную цепочку fallback для температуры CPU (5 приоритетов):
1. Core/Package/Tctl/Tdie/CCD sensors из CPU
2. Motherboard sensors (ASUS/CPUTIN)
3. Любые CPU temperature sensors
4. Global search по всем сенсорам
5. `Win32_PerfFormattedData_Counters_ThermalZoneInformation` (`root\cimv2`) — работает **без прав администратора**; поле `HighPrecisionTemperature` в дека-Кельвинах (÷10 - 273.15)

При отладке сенсоров проверяйте `BuildSensorMap()` и sensor type matching логику.

## Project-Specific Conventions

### Language
Весь UI, комментарии и сообщения на **русском языке**, включая названия метрик и tooltip тексты.

### Visual Design Patterns
- **Progressive Gradient System**: бары меняют градиент по мере роста значения (зелёный → жёлтый → красный)
- **Integer Display Only**: `MetricItem.Value` привязан с `StringFormat={}{0:0}` в XAML (без дробной части)
- **Snap Threshold**: `16 DIP` для прилипания к краям экрана при drag

### Window Behavior
- **Always Behind**: `SetWindowPos(hwnd, HWND_BOTTOM, ...)` в `OnLoaded` держит окно позади всех остальных
- **No Taskbar**: `ShowInTaskbar="False"` в XAML
- **Position Persistence**: сохраняется в `%LocalAppData%\HardwareMonitorWidget\window-position.json`
- **Multi-Monitor Clamping**: `ClampToNearestMonitorWorkArea()` использует Win32 `MonitorFromPoint`/`GetMonitorInfo` для корректного восстановления позиции
- **Startup Status Display**: при ошибке автозапуска отображается оранжевый warning-badge (⚠ `#FF9A2F`, рамка `#B35C00`, фон `#120900`) через `StartupStatusVisible` (Visibility.Collapsed при успехе); ToolTip показывает детали

### GPU Selection Logic
`SelectGpu()` в `LibreHardwareMonitorService`:
1. Если есть discrete GPU (NVIDIA/AMD) с load > 0.5%, выбирается наиболее загруженный
2. Иначе возвращается первый доступный GPU
3. Integrated Intel GPUs имеют низший приоритет

## Integration Points

### LibreHardwareMonitor
- Инициализация: `Computer` должен иметь `IsCpuEnabled`, `IsMemoryEnabled`, `IsGpuEnabled`, `IsMotherboardEnabled`
- Обязательно вызывать `UpdateHardwareRecursive()` перед чтением сенсоров
- Sensor filtering: `SensorType.Load` для процентов, `SensorType.Temperature` для температуры

### Task Scheduler Autostart
`TaskSchedulerStartupRegistrationService` использует `schtasks` CLI (не registry):
```powershell
schtasks /Create /TN "HardwareMonitorWidget" /TR "\"path\to\exe\"" /SC ONLOGON /RL HIGHEST /F
```
- Timeout: 10 секунд
- Privilege level: `HIGHEST` (требуется для LibreHardwareMonitor)
- Trigger: `ONLOGON` для текущего пользователя
- **Manifest**: `requireAdministrator` в app.manifest для доступа к аппаратным датчикам

### Win32 APIs
P/Invoke signatures в `MainWindow.xaml.cs`:
- `SetWindowPos`: позиционирование окна
- `MonitorFromPoint`: определение ближайшего монитора
- `GetMonitorInfo`: получение work area bounds

DPI-aware через `VisualTreeHelper.GetTransform()` для конвертации DIP ↔ Physical Pixels.

## Dependencies
```xml
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
<PackageReference Include="LibreHardwareMonitorLib" Version="0.9.5" />
<PackageReference Include="System.Management" Version="10.0.1" />
```

Все зависимости стабильны; избегайте breaking changes при обновлении.

## Common Pitfalls
- **Don't block UI thread**: hardware polling всегда через `Task.Run()` в `RefreshTargetsAsync()`
- **Freeze brushes**: все brush объекты должны быть `.Freeze()` перед использованием в palette
- **JSON serialization**: `WindowPositionState` record требует простые типы (double) для `System.Text.Json`
- **Temperature units**: LibreHardwareMonitor возвращает °C; `Win32_PerfFormattedData_Counters_ThermalZoneInformation.HighPrecisionTemperature` возвращает deci-Kelvin (÷10 - 273.15)
