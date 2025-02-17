using Blazicons;

namespace VRCDollyManager.Models;

public class NotificationMessage(string message, SvgIcon icon, NotificationType type) : EventArgs
{
    public string Message { get; } = message;
    public SvgIcon Icon { get; } = icon;
    public NotificationType Type { get; } = type;
}