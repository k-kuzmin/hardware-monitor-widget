# Hardware Monitor Widget

Компактный cyberpunk WPF-виджет для мониторинга CPU/RAM/GPU в реальном времени.

## Стек

- WPF (.NET 8)
- LibreHardwareMonitor
- CommunityToolkit.Mvvm

## Возможности

- Отображение метрик: `CPU Load`, `CPU Temp`, `GPU Load`, `GPU Temp`, `RAM Load`
- Обновление данных: 1 раз в секунду
- Плавная анимация баров: 700 мс (частота анимации ~20 FPS, 50 мс)
- Компактный layout в 2 колонки
- Виджет без рамки окна, отображается позади обычных окон
- Перетаскивание виджета мышью
- Прилипание к краям рабочего стола при перетаскивании
- Сохранение позиции окна между запусками
- Корректное восстановление позиции при нескольких мониторах (если доп. экран отключен, окно переносится в видимую область)
- Фиксированный размер без ресайза и без скролла
- Значения показываются целыми числами (без дробной части)
- Прогрессивный составной градиент бара по мере роста значения:
	- низкие значения — зелёный диапазон
	- средние — добавляется жёлтый
	- высокие — добавляется красный
- Текстовые значения метрик окрашиваются сплошным цветом (без градиента), зависящим от уровня нагрузки/температуры
- Кэширование палитры кистей для снижения аллокаций в UI-цикле
- Для температуры CPU: 5-уровневый fallback (LHM-сенсоры → материнская плата → любые CPU-сенсоры → глобальный поиск → `Win32_PerfFormattedData_Counters_ThermalZoneInformation`)
- WMI-fallback работает без прав администратора

## Скриншот

![Widget Screenshot](docs/widget-screenshot.png)

## Запуск

```bash
dotnet restore
dotnet build HardwareMonitorWidget.sln
dotnet run --project src/HardwareMonitorWidget/HardwareMonitorWidget.csproj
```

## Автозапуск

При старте приложение пытается зарегистрировать machine-wide задачу в Task Scheduler:

- имя задачи: `HardwareMonitorWidget`
- триггер: `ONLOGON`
- команда: запуск текущего `.exe`
- уровень привилегий: `HIGHEST` (требуется для LibreHardwareMonitor)
- таймаут регистрации задачи: 10 секунд

При ошибке регистрации в шапке виджета появляется оранжевый warning-badge ⚠ с кратким статусом; подробности доступны в tooltip.
Если прав недостаточно, запустите приложение один раз от имени администратора.

## Релизная сборка

Сборка решения в режиме Release:

```bash
dotnet build HardwareMonitorWidget.sln -c Release -v minimal
```

Публикация исполняемого приложения (framework-dependent, `win-x64`):

```bash
dotnet publish src/HardwareMonitorWidget/HardwareMonitorWidget.csproj -c Release -r win-x64 --self-contained false -o artifacts/release/win-x64
```
