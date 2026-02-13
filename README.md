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

### Если Windows Defender блокирует запуск (`0x800700E1`)

Выполните в PowerShell от имени администратора:

```powershell
Add-MpPreference -ExclusionPath "D:\Portfolio\Projects\hardware-monitor-widget"
Add-MpPreference -ExclusionProcess "dotnet.exe"
Add-MpPreference -ExclusionProcess "rider64.exe"
```

После этого повторите команды из раздела «Запуск».

### Проверка, что исключения применились

```powershell
$mp = Get-MpPreference
$mp.ExclusionPath | Where-Object { $_ -eq "D:\Portfolio\Projects\hardware-monitor-widget" }
$mp.ExclusionProcess | Where-Object { $_ -match "dotnet\.exe|rider64\.exe" }
```

Если команды вернули путь проекта и оба процесса, исключения применены.

## Автозапуск

Приложение пытается зарегистрировать machine-wide задачу в Task Scheduler при запуске:

- имя задачи: `HardwareMonitorWidget`
- триггер: `ONLOGON`
- команда: запуск текущего `.exe`

Если прав недостаточно, регистрация может не выполниться. В этом случае запустите приложение один раз от имени администратора.
