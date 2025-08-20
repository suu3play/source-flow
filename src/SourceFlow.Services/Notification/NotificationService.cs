using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SourceFlow.Core.Interfaces;
using SourceFlow.Core.Models;
using SourceFlow.Data.Context;
using SourceFlow.Data.Models;
using SourceFlow.Services.Settings;

namespace SourceFlow.Services.Notification;

public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;
    private readonly SourceFlowDbContext _context;
    private readonly IApplicationSettingsService _settingsService;
    private NotificationSettings _settings = new();

    // Windows Toast Notification用のWin32 API
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA pnid);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
    }

    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIM_DELETE = 0x00000002;
    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;
    private const uint NIF_INFO = 0x00000010;
    private const uint NIIF_INFO = 0x00000001;
    private const uint NIIF_WARNING = 0x00000002;
    private const uint NIIF_ERROR = 0x00000003;

    public NotificationService(
        ILogger<NotificationService> logger,
        SourceFlowDbContext context,
        IApplicationSettingsService settingsService)
    {
        _logger = logger;
        _context = context;
        _settingsService = settingsService;
        LoadSettingsAsync();
    }

    private async void LoadSettingsAsync()
    {
        try
        {
            var settings = await _settingsService.LoadSettingsAsync();
            _settings = settings.Notifications;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "通知設定の読み込みに失敗しました。デフォルト設定を使用します。");
            _settings = new NotificationSettings();
        }
    }

    public async Task ShowNotificationAsync(NotificationRequest request)
    {
        try
        {
            // 設定確認
            if (!_settings.EnableDesktopNotifications)
            {
                _logger.LogDebug("デスクトップ通知が無効化されています");
                return;
            }

            if (!IsNotificationEnabled(request.Type))
            {
                _logger.LogDebug("通知タイプ {Type} が無効化されています", request.Type);
                return;
            }

            // Windows Toast通知を表示
            await ShowWindowsToastNotificationAsync(request);

            // 音声通知
            if (_settings.EnableSoundNotifications && request.PlaySound)
            {
                PlayNotificationSound(request.Type);
            }

            // 履歴保存
            if (_settings.SaveNotificationHistory)
            {
                await SaveNotificationHistoryAsync(request);
            }

            _logger.LogInformation("通知を表示しました: {Title}", request.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "通知の表示に失敗しました: {Title}", request.Title);
        }
    }

    public async Task ShowReleaseCompletedNotificationAsync(string releaseName, int filesProcessed, TimeSpan duration)
    {
        var request = new NotificationRequest
        {
            Title = "リリース完了",
            Message = $"リリース '{releaseName}' が完了しました。\n処理ファイル数: {filesProcessed}件\n処理時間: {duration:mm\\:ss}",
            Type = NotificationType.Success,
            Priority = NotificationPriority.Normal,
            Data = new Dictionary<string, object>
            {
                ["ReleaseName"] = releaseName,
                ["FilesProcessed"] = filesProcessed,
                ["Duration"] = duration.TotalSeconds
            }
        };

        await ShowNotificationAsync(request);
    }

    public async Task ShowReleaseErrorNotificationAsync(string releaseName, string errorMessage, int errorCount)
    {
        var request = new NotificationRequest
        {
            Title = "リリースエラー",
            Message = $"リリース '{releaseName}' でエラーが発生しました。\nエラー数: {errorCount}件\n詳細: {errorMessage}",
            Type = NotificationType.Error,
            Priority = NotificationPriority.High,
            Data = new Dictionary<string, object>
            {
                ["ReleaseName"] = releaseName,
                ["ErrorMessage"] = errorMessage,
                ["ErrorCount"] = errorCount
            }
        };

        await ShowNotificationAsync(request);
    }

    public async Task ShowSyncCompletedNotificationAsync(string syncName, int filesProcessed)
    {
        var request = new NotificationRequest
        {
            Title = "同期完了",
            Message = $"同期 '{syncName}' が完了しました。\n処理ファイル数: {filesProcessed}件",
            Type = NotificationType.Success,
            Priority = NotificationPriority.Normal,
            Data = new Dictionary<string, object>
            {
                ["SyncName"] = syncName,
                ["FilesProcessed"] = filesProcessed
            }
        };

        await ShowNotificationAsync(request);
    }

    public async Task ShowSyncErrorNotificationAsync(string syncName, string errorMessage)
    {
        var request = new NotificationRequest
        {
            Title = "同期エラー",
            Message = $"同期 '{syncName}' でエラーが発生しました。\n詳細: {errorMessage}",
            Type = NotificationType.Error,
            Priority = NotificationPriority.High,
            Data = new Dictionary<string, object>
            {
                ["SyncName"] = syncName,
                ["ErrorMessage"] = errorMessage
            }
        };

        await ShowNotificationAsync(request);
    }

    public async Task<List<NotificationHistory>> GetNotificationHistoryAsync(int limit = 50)
    {
        try
        {
            var entities = await _context.NotificationHistory
                .OrderByDescending(n => n.CreatedAt)
                .Take(limit)
                .ToListAsync();

            return entities.Select(e => new NotificationHistory
            {
                Id = e.Id,
                Title = e.Title,
                Message = e.Message,
                Type = Enum.TryParse<NotificationType>(e.Type, out var type) ? type : NotificationType.Info,
                Priority = Enum.TryParse<NotificationPriority>(e.Priority, out var priority) ? priority : NotificationPriority.Normal,
                CreatedAt = e.CreatedAt,
                IsRead = e.IsRead,
                Data = e.Data
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "通知履歴の取得に失敗しました");
            return new List<NotificationHistory>();
        }
    }

    public async Task<bool> MarkNotificationAsReadAsync(int notificationId)
    {
        try
        {
            var notification = await _context.NotificationHistory.FindAsync(notificationId);
            if (notification != null)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "通知の既読化に失敗しました: ID={Id}", notificationId);
            return false;
        }
    }

    public async Task<bool> ClearNotificationHistoryAsync()
    {
        try
        {
            var cutoffDate = DateTime.Now.AddDays(-_settings.MaxHistoryDays);
            
            // 期限切れ通知を削除
            var expiredNotifications = await _context.NotificationHistory
                .Where(n => n.CreatedAt < cutoffDate)
                .ToListAsync();
            
            _context.NotificationHistory.RemoveRange(expiredNotifications);
            
            // 最大件数を超えている場合は古いものから削除
            var totalCount = await _context.NotificationHistory.CountAsync();
            if (totalCount > _settings.MaxHistoryCount)
            {
                var excessCount = totalCount - _settings.MaxHistoryCount;
                var oldestNotifications = await _context.NotificationHistory
                    .OrderBy(n => n.CreatedAt)
                    .Take(excessCount)
                    .ToListAsync();
                
                _context.NotificationHistory.RemoveRange(oldestNotifications);
            }
            
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "通知履歴のクリアに失敗しました");
            return false;
        }
    }

    public async Task<int> GetUnreadNotificationCountAsync()
    {
        try
        {
            return await _context.NotificationHistory.CountAsync(n => !n.IsRead);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "未読通知数の取得に失敗しました");
            return 0;
        }
    }

    public bool IsNotificationEnabled(NotificationType type)
    {
        return type switch
        {
            NotificationType.Info => _settings.ShowInfoNotifications,
            NotificationType.Success => _settings.ShowReleaseCompletedNotifications || _settings.ShowSyncCompletedNotifications,
            NotificationType.Warning => _settings.ShowWarningNotifications,
            NotificationType.Error => _settings.ShowReleaseErrorNotifications || _settings.ShowSyncErrorNotifications,
            _ => true
        };
    }

    public bool IsSoundEnabled()
    {
        return _settings.EnableSoundNotifications;
    }

    private async Task ShowWindowsToastNotificationAsync(NotificationRequest request)
    {
        try
        {
            // Win32 APIを使ったバルーン通知
            var nid = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = GetMainWindowHandle(),
                uID = 1000,
                uFlags = NIF_INFO,
                szInfoTitle = request.Title,
                szInfo = request.Message,
                uTimeoutOrVersion = (uint)(_settings.NotificationDurationSeconds * 1000),
                dwInfoFlags = GetNotificationIcon(request.Type)
            };

            Shell_NotifyIcon(NIM_MODIFY, ref nid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Windows通知の表示に失敗しました");
        }

        await Task.CompletedTask;
    }

    private IntPtr GetMainWindowHandle()
    {
        // メインウィンドウのハンドルを取得
        var handle = FindWindow(null, "SourceFlow");
        return handle != IntPtr.Zero ? handle : IntPtr.Zero;
    }

    private uint GetNotificationIcon(NotificationType type)
    {
        return type switch
        {
            NotificationType.Success => NIIF_INFO,
            NotificationType.Warning => NIIF_WARNING,
            NotificationType.Error => NIIF_ERROR,
            _ => NIIF_INFO
        };
    }

    private void PlayNotificationSound(NotificationType type)
    {
        try
        {
            // Windows APIを使用してシステム音を再生
            uint soundType = type switch
            {
                NotificationType.Success => 0x00000040, // MB_ICONASTERISK
                NotificationType.Warning => 0x00000030, // MB_ICONEXCLAMATION  
                NotificationType.Error => 0x00000010, // MB_ICONHAND
                _ => 0x00000000 // MB_OK
            };
            
            MessageBeep(soundType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "通知音の再生に失敗しました");
        }
    }

    [DllImport("user32.dll")]
    private static extern bool MessageBeep(uint uType);

    private string GetSoundFile(NotificationType type)
    {
        return type switch
        {
            NotificationType.Success => _settings.SuccessSoundFile,
            NotificationType.Warning => _settings.WarningSoundFile,
            NotificationType.Error => _settings.ErrorSoundFile,
            _ => _settings.InfoSoundFile
        };
    }

    private async Task SaveNotificationHistoryAsync(NotificationRequest request)
    {
        try
        {
            var entity = new NotificationHistoryEntity
            {
                Title = request.Title,
                Message = request.Message,
                Type = request.Type.ToString(),
                Priority = request.Priority.ToString(),
                CreatedAt = DateTime.Now,
                IsRead = false,
                Data = request.Data
            };

            _context.NotificationHistory.Add(entity);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "通知履歴の保存に失敗しました");
        }
    }
}