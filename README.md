# Hardware Monitor Widget

Минималистичный WPF-виджет для мониторинга CPU/RAM/GPU.

## Стек

- WPF
- LibreHardwareMonitor
- LiveCharts2
- CommunityToolkit.Mvvm

## Что показывает

- `CPU Load` (%)
- `CPU Temp` (°C)
- `RAM Load` (%)
- `GPU Load` (%)
- `GPU Temp` (°C)

Обновление данных происходит раз в 1 секунду.
Анимация баров — линейная, 700мс.

## Запуск

```bash
dotnet restore
dotnet build HardwareMonitorWidget.sln
dotnet run --project src/HardwareMonitorWidget/HardwareMonitorWidget.csproj
```

## Автозапуск

Приложение пытается зарегистрировать machine-wide задачу в Task Scheduler при запуске:

- имя задачи: `HardwareMonitorWidget`
- триггер: `ONLOGON`
- команда: запуск текущего `.exe`

Если прав недостаточно, регистрация может не выполниться. В этом случае запустите приложение один раз от имени администратора.
