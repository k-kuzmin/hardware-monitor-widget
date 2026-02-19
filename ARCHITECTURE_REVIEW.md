# Архитектурный обзор — SOLID

Дата: 2026-02-19

---

## Общая картина

~8 исходных файлов, ~1000 строк кода. Для проекта такого масштаба архитектура вполне достойная — интерфейсы выделены, MVVM соблюдается, зависимости инжектятся через конструктор. Но есть конкретные точки роста.

---

## S — Single Responsibility

### MainWindow — главный нарушитель (~289 строк)

`MainWindow.xaml.cs` тащит на себе **5 ролей**:

1. **Composition Root** — создаёт сервисы и ViewModel
2. **Win32 Interop** — P/Invoke (`SetWindowPos`, `MonitorFromPoint`, `GetMonitorInfo`) + 3 нативных структуры
3. **Координатная математика** — DIP ↔ Physical Pixels
4. **Persistence позиции** — чтение/запись JSON в `%LocalAppData%`
5. **Edge Snapping** — алгоритм привязки к краям экрана

**Рекомендация:** вынести `WindowPositionService` (persistence + clamping + snapping) и `Win32Interop` (P/Invoke + структуры) в отдельные классы. MainWindow останется тонким хостом.

### MainViewModel (~322 строк) — умеренное нарушение

Совмещает:
- Двойной цикл опроса/анимации
- Анимационный движок (интерполяция, палитра кистей)
- Логику обратной связи автозапуска (`StartupStatus*` свойства)

**Рекомендация:** выделить `MetricAnimator` (палитра + lerp). `StartupStatus` — в отдельный partial-класс или мини-VM.

### LibreHardwareMonitorService (~293 строк) — на грани

Делает многое: инициализация LHM, рекурсивный обход сенсоров, GPU-selection, 5-уровневый fallback температуры CPU, P/Invoke для RAM (`GlobalMemoryStatusEx`), WMI-запрос. Но всё это — единая задача: «собрать снимок железа». SRP скорее соблюдён по духу, хотя по букве можно выделить стратегии для каждой метрики.

---

## O — Open/Closed

### Метрики захардкожены — главная проблема OCP

В `MainViewModel.cs` метрики создаются жёстким списком:

```csharp
Metrics = new ObservableCollection<MetricItem>
{
    new("CPU Load", "%"),
    new("CPU Temp", "°C"),
    ...
};
```

А в `SetTargetsFromSnapshot` — маппинг по индексу (`Metrics[0]`, `Metrics[1]`, ...). Добавление нового датчика требует правки в **двух местах** ViewModel и в `HardwareSnapshot`.

**Рекомендация:** data-driven подход — массив конфигов метрик или enum-based маппинг, чтобы добавление нового датчика не трогало существующий код.

---

## L — Liskov Substitution

**Нарушений нет.** Наследования нет (кроме framework-классов). Record `HardwareSnapshot` и `ObservableObject`-наследники ведут себя корректно.

---

## I — Interface Segregation

**Отлично.** Оба интерфейса минимальны:

- `IHardwareMonitorService` — один метод `ReadSnapshot()`
- `IStartupRegistrationService` — один метод `EnsureMachineWideAutostartAsync()`

Ни один клиент не зависит от методов, которые не использует.

---

## D — Dependency Inversion

### Хорошо: ViewModel зависит от абстракций

```csharp
public MainViewModel(
    IHardwareMonitorService hardwareMonitorService,
    IStartupRegistrationService startupRegistrationService)
```

ViewModel полностью тестируем через моки — правильная инверсия.

### Нюанс: MainWindow — ручной composition root

```csharp
var hardwareMonitorService = new LibreHardwareMonitorService();
var startupRegistrationService = new TaskSchedulerStartupRegistrationService();
```

`new` конкретных классов в `MainWindow.xaml.cs`. Для проекта без DI-контейнера это осознанный trade-off. Если проект вырастет — стоит подключить `Microsoft.Extensions.DependencyInjection`.

### Проблема: MetricItem держит WPF-типы

`MetricItem.cs` хранит `System.Windows.Media.Brush` — презентационный тип в модели. Нарушает направление зависимостей: модельный слой зависит от UI-фреймворка.

**Рекомендация:** `MetricItem` хранит только `double Value`, а конвертация в кисти — через `IValueConverter` в XAML или через ViewModel-обёртку.

---

## Дополнительные замечания

| Что | Проблема | Рекомендация |
|-----|----------|--------------|
| `ReadSnapshot()` синхронный | Интерфейс скрывает блокирующий I/O; `Task.Run` на стороне вызывающего | Сделать `Task<HardwareSnapshot> ReadSnapshotAsync()` — честный контракт |
| `_pollsSinceReinit` в ReadSnapshot | Скрытый side-effect у метода чтения | Выделить `ReinitializeIfNeeded()` как отдельный явный шаг |
| Нет unit-тестов | Архитектура позволяет тестировать ViewModel, но тестов нет | Добавить хотя бы smoke-тесты для ViewModel с мок-сервисом |

---

## Итоговая оценка

| Принцип | Оценка | Комментарий |
|---------|--------|-------------|
| **S** | 6/10 | MainWindow перегружен, ViewModel на грани |
| **O** | 5/10 | Метрики захардкожены, расширение требует правки |
| **L** | 10/10 | Нет нарушений |
| **I** | 10/10 | Интерфейсы минимальны и точны |
| **D** | 7/10 | ViewModel правильный, но Brush в модели нарушает слои |

**Общий вердикт:** для десктоп-виджета на ~1000 строк — хорошая архитектура. Интерфейсы, MVVM, разделение на сервисы — всё на месте. Главные точки роста: разгрузить MainWindow, убрать WPF-типы из модели и сделать метрики data-driven.
