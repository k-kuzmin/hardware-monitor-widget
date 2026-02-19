using System.Diagnostics;
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

    public async Task RegisterAsync(IStartupRegistrationService registrationService)
    {
        var executablePath = Process.GetCurrentProcess().MainModule?.FileName;
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
        catch
        {
            Status = "Автозапуск: ошибка";
            StatusDetails = "Произошла ошибка при регистрации автозапуска.";
            IsVisible = Visibility.Visible;
        }
    }
}
