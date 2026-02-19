# План рефакторинга — Полный SOLID

Дата: 2026-02-19

## Контекст

Устранить все нарушения из `ARCHITECTURE_REVIEW.md`. Новых функций не добавляется — только реструктуризация. Каждый шаг атомарен: после него проект компилируется.

---

## Целевая файловая структура

```
src/HardwareMonitorWidget/
  Infrastructure/
    Win32/
      Win32Api.cs                   # P/Invoke: SetWindowPos, MonitorFromPoint, GetMonitorInfo + POINT/RECT/MONITORINFO
    WindowPositionService.cs        # persistence (JSON), clamping, snap-to-edge
  Models/
    HardwareSnapshot.cs             # без изменений
    MetricDefinition.cs             # NEW: Id-enum + Label + Unit + Func<HardwareSnapshot,double> Selector
    MetricItem.cs                   # чистая модель: Label, Unit, double Value (без Brush)
  ViewModels/
    MetricViewModel.cs              # NEW: ObservableObject — MetricItem + BarBrush + TextBrush
    MetricAnimator.cs               # NEW: BrushPalette[101], lerp, обновление MetricViewModel
    StartupStatusViewModel.cs       # NEW: StartupStatus, StartupStatusDetails, StartupStatusVisible + RegisterAsync
    MainViewModel.cs                # тонкий: двойной цикл + склейка MetricDefinition → MetricViewModel
  Services/
    IHardwareMonitorService.cs      # async: Task<HardwareSnapshot> ReadSnapshotAsync(CancellationToken)
    LibreHardwareMonitorService.cs  # реализует async + явный ReinitializeIfNeeded()
    IStartupRegistrationService.cs  # без изменений
    TaskSchedulerStartupRegistrationService.cs  # без изменений
  MainWindow.xaml.cs                # тонкий ~60 строк: composition root + SetWindowPos + делегирует сервисам
```

---

## Шаги

### Шаг 1 — Вынести Win32-слой

Создать `Infrastructure/Win32/Win32Api.cs` (`internal static`).
Перенести из `MainWindow.xaml.cs`:
- Три `[DllImport]`-сигнатуры: `SetWindowPos`, `MonitorFromPoint`, `GetMonitorInfo`
- Три struct: `POINT`, `RECT`, `MONITORINFO`
- Константы: `HWND_BOTTOM`, `SWP_NOSIZE`, `SWP_NOMOVE`, `SWP_NOACTIVATE`, `MONITOR_DEFAULTTONEAREST`

### Шаг 2 — Вынести WindowPositionService

Создать `Infrastructure/WindowPositionService.cs`.
Перенести из `MainWindow.xaml.cs`:
- `SaveWindowPosition`, `TryRestoreWindowPosition`
- `SnapToNearestMonitorEdges`, `ClampToNearestMonitorWorkArea`, `GetNearestMonitorWorkArea`
- `DipPointToPixel`, `PixelRectToDip`, `GetTransformToDevice`
- `GetWindowWidthForBounds`, `GetWindowHeightForBounds`, `ClampToRange`
- `WindowPositionState` record

Зависимость на `Window` принимается через конструктор.

### Шаг 3 — Облегчить MainWindow.xaml.cs

После шагов 1–2 оставить только:
- Конструктор: создание сервисов + ViewModel (composition root)
- `OnLoaded`: `SetWindowPos` + `InitializeAsync`
- `OnClosed`: `WindowPositionService.Save()` + `DisposeAsync`
- `OnRootMouseLeftButtonDown`: делегирует `WindowPositionService`

Целевой объём — ~60 строк.

### Шаг 4 — Вынести MetricAnimator

Создать `ViewModels/MetricAnimator.cs`.
Перенести из `MainViewModel.cs`:
- Статические палитры `BarBrushPalette[101]`, `TextBrushPalette[101]` и их создание
- `CreateProgressiveBarBrush`, `CreateProgressiveTextColor`, `LerpColor`
- `GetBarBrush`, `GetTextBrush`
- Логику `ApplyInterpolatedValues` → публичный метод `UpdateFrame(metrics, start, current, target, animStart)`

`MainViewModel` хранит `private readonly MetricAnimator _animator` и вызывает его в анимационном цикле.

### Шаг 5 — Вынести StartupStatusViewModel

Создать `ViewModels/StartupStatusViewModel.cs`.
Перенести из `MainViewModel.cs`:
- Свойства: `StartupStatus`, `StartupStatusDetails`, `StartupStatusVisible`
- Метод `RegisterAsync(IStartupRegistrationService, string executablePath)`

`MainViewModel` хранит `public StartupStatusViewModel StartupStatus { get; }`.
XAML биндинги обновить: `StartupStatus.StartupStatusVisible` и т.д.

### Шаг 6 — Разделить MetricItem → модель + MetricViewModel

**`Models/MetricItem.cs`** — убрать `using System.Windows.Media`, убрать `BarBrush` и `TextBrush`.
Оставить: `Label`, `Unit`, `double Value`, `DisplayValue`. Можно сделать `record`.

**`ViewModels/MetricViewModel.cs`** (NEW) — `ObservableObject` с:
- `string Label`, `string Unit`, `double Value`, `string DisplayValue`
- `Brush BarBrush`, `Brush TextBrush`

Обновить биндинги в `MainWindow.xaml` — `ObservableCollection<MetricViewModel>`.

### Шаг 7 — Сделать метрики data-driven

Создать `Models/MetricDefinition.cs`:

```csharp
public enum MetricId { CpuLoad, CpuTemp, GpuLoad, GpuTemp, RamLoad }

public sealed record MetricDefinition(
    MetricId Id,
    string Label,
    string Unit,
    Func<HardwareSnapshot, double> Selector)
{
    public static readonly MetricDefinition[] All =
    [
        new(MetricId.CpuLoad, "CPU Load", "%",  s => s.CpuLoad),
        new(MetricId.CpuTemp, "CPU Temp", "°C", s => s.CpuTemperature),
        new(MetricId.GpuLoad, "GPU Load", "%",  s => s.GpuLoad),
        new(MetricId.GpuTemp, "GPU Temp", "°C", s => s.GpuTemperature),
        new(MetricId.RamLoad, "RAM Load", "%",  s => s.RamLoad),
    ];
}
```

В `MainViewModel`:
- `Metrics` инициализируется через `MetricDefinition.All.Select(d => new MetricViewModel(d))`
- `SetTargetsFromSnapshot` — цикл: `_targetValues[i] = _definitions[i].Selector(snapshot)`
- Магические `Metrics[0]..Metrics[4]` и `SetNewTarget(0, snapshot.CpuLoad)` исчезают

Добавление нового датчика = одна строка в `MetricDefinition.All` + поле в `HardwareSnapshot`.

### Шаг 8 — Асинхронизировать IHardwareMonitorService

**`IHardwareMonitorService.cs`:**
```csharp
Task<HardwareSnapshot> ReadSnapshotAsync(CancellationToken ct = default);
```

**`LibreHardwareMonitorService.cs`:**
- `ReadSnapshotAsync` → `return Task.Run(() => ReadSnapshotCore(), ct)`
- Скрытый side-effect `_pollsSinceReinit` становится явным вызовом `ReinitializeIfNeeded()` в начале `ReadSnapshotCore()`

**`MainViewModel.RefreshTargetsAsync`:**
- Убрать `Task.Run(() => _hardwareMonitorService.ReadSnapshot())`
- Заменить на `await _hardwareMonitorService.ReadSnapshotAsync(cancellationToken)`

---

## Верификация

После каждого шага:
```powershell
dotnet build HardwareMonitorWidget.sln
```

После всех шагов:
```powershell
dotnet run --project src/HardwareMonitorWidget/HardwareMonitorWidget.csproj
```

Проверить: виджет запускается, метрики обновляются, drag + snap к краям работает, автозапуск регистрируется.

---

## Принятые решения

| Тема | Решение |
|------|---------|
| Brush в модели | MetricViewModel-обёртка — MetricItem без WPF-зависимости, Brush живёт в VM-слое |
| DI-контейнер | Не добавляем — manual composition root в MainWindow, но облегчённый (~10 строк) |
| Unit-тесты | Не входят в этот план |
| Task.Run | Перемещается из ViewModel в реализацию сервиса — контракт интерфейса становится честным |
