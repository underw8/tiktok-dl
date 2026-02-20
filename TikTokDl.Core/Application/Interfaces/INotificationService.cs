namespace TikTokDl.Core.Application.Interfaces;

/// <summary>
/// Cross-platform notification service
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Shows a notification to the user
    /// </summary>
    Task ShowNotificationAsync(string title, string message, NotificationType type = NotificationType.Info);
    
    /// <summary>
    /// Gets or sets whether notifications are enabled
    /// </summary>
    bool IsEnabled { get; set; }
}

/// <summary>
/// Types of notifications
/// </summary>
public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error
}
