namespace SourceFlow.Core.Interfaces;

public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error
}

public enum NotificationPriority
{
    Low,
    Normal,
    High,
    Critical
}

public class NotificationRequest
{
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public NotificationType Type { get; set; } = NotificationType.Info;
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    public bool PlaySound { get; set; } = true;
    public TimeSpan? Duration { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}

public class NotificationHistory
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public NotificationType Type { get; set; }
    public NotificationPriority Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}

public interface INotificationService
{
    Task ShowNotificationAsync(NotificationRequest request);
    Task ShowReleaseCompletedNotificationAsync(string releaseName, int filesProcessed, TimeSpan duration);
    Task ShowReleaseErrorNotificationAsync(string releaseName, string errorMessage, int errorCount);
    Task ShowSyncCompletedNotificationAsync(string syncName, int filesProcessed);
    Task ShowSyncErrorNotificationAsync(string syncName, string errorMessage);
    Task<List<NotificationHistory>> GetNotificationHistoryAsync(int limit = 50);
    Task<bool> MarkNotificationAsReadAsync(int notificationId);
    Task<bool> ClearNotificationHistoryAsync();
    Task<int> GetUnreadNotificationCountAsync();
    bool IsNotificationEnabled(NotificationType type);
    bool IsSoundEnabled();
}