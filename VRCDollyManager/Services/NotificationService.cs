using Blazicons;
using VRCDollyManager.Models;

namespace VRCDollyManager.Services;

public class NotificationService
{
    public delegate void OnNotificationEventHandler(NotificationMessage e);

    public event OnNotificationEventHandler? OnNotification;

    public void ShowNotification(string message, SvgIcon icon, NotificationType type = NotificationType.Info)
    {
        OnNotification?.Invoke(new NotificationMessage(message, icon, type));
    }
}