using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using HardwareMonitorWidget.Services;

namespace HardwareMonitorWidget.ViewModels;

public partial class StartupStatusViewModel : ObservableObject
{
    [ObservableProperty]
    private string _status = "Автозапуск: настройка";

    [ObservableProperty]
    private string _statusDetails = "Инициализация автозапуска...";

    [ObservableProperty]
    private Visibility _isVisible = Visibility.Collapsed;

    public async Task RegisterAsync(IStartupRegistrationService registrationService, string? executablePath)
    {
        // ARCH-03: путь передаётся снаружи — ViewModel не читает Process
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            Status = "Автозапуск: нет пути";
            StatusDetails = "Не найден путь к исполняемому файлу приложения.";
            IsVisible = Visibility.Visible;
            return;
        }

        try
        {
            var registered = await registrationService.EnsureMachineWideAutostartAsync(executablePath);
            if (registered)
            {
                // Успех — скрываем статус
                IsVisible = Visibility.Collapsed;
            }
            else
            {
                Status = "Автозапуск: нет прав";
                StatusDetails = "Не удалось настроить автозапуск. Попробуйте запустить приложение от администратора.";
                IsVisible = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            // CQ-09: включаем детали исключения для диагностики
            Status = "Автозапуск: ошибка";
            StatusDetails = $"Ошибка регистрации: {ex.GetType().Name}: {ex.Message}";
            IsVisible = Visibility.Visible;
        }
    }
}
