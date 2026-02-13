## Описание
Нужно создать виджет мониторинга систем компьютера (процессор, память, видеокарта и другое).

## Стек технологий
WPF + LibreHardwareMonitor + LiveCharts2 + CommunityToolkit.Mvvm

## Зафиксированные решения
- автозапуск: Task Scheduler (machine-wide, для любого пользователя)
- UI первой версии: только бары + подписи + числа
- сглаживание: линейная анимация 700мс при опросе 1с
- GPU: активная дискретная, иначе первая доступная

## План реализации (чекбокс)
- [ ] Создать каркас решения и WPF-приложения: `HardwareMonitorWidget.sln`, `src/HardwareMonitorWidget/HardwareMonitorWidget.csproj`, `src/HardwareMonitorWidget/App.xaml`, `src/HardwareMonitorWidget/App.xaml.cs`
- [ ] Подключить зависимости (LibreHardwareMonitor, LiveCharts2, CommunityToolkit.Mvvm) и базовые настройки проекта
- [ ] Создать MVVM-структуру и контракты сервисов: `MainViewModel`, `MetricItem`, `IHardwareMonitorService`, `IStartupRegistrationService`
- [ ] Реализовать чтение сенсоров CPU/RAM/GPU через LibreHardwareMonitor
- [ ] Настроить выбор GPU: активная дискретная, иначе первая доступная
- [ ] Реализовать polling раз в 1 секунду и безопасное обновление ViewModel
- [ ] Добавить плавное изменение значений (linear, 700мс)
- [ ] Собрать минималистичный UI: подписи, бары и числовые значения для CPU/RAM/GPU
- [ ] Сделать градиент баров от зеленого к красному по уровню нагрузки/температуры
- [ ] Реализовать machine-wide автозапуск через Task Scheduler
- [ ] Подключить инициализацию сервисов и корректное освобождение ресурсов при закрытии приложения
- [ ] Добавить документацию по запуску и автозапуску в `README.md`
- [ ] Проверить: `dotnet restore`, `dotnet build`, ручная валидация UI, сенсоров и автозапуска