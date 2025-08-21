using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Spi;
using SourceFlow.Core.Interfaces;
using SourceFlow.Services.Configuration;
using SourceFlow.Services.Sync;
using SourceFlow.Services.Comparison;
using SourceFlow.Services.Database;
using SourceFlow.Services.Settings;
using SourceFlow.Services.Release;
using SourceFlow.Services.Notification;
using SourceFlow.Services.Diff;
using SourceFlow.Services.Schedule;

namespace SourceFlow.Services.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddScoped<ISourceSyncService, LocalSyncService>();
        services.AddScoped<IComparisonService, ComparisonService>();
        services.AddScoped<IDatabaseService, DatabaseService>();
        services.AddSingleton<IApplicationSettingsService, ApplicationSettingsService>();
        services.AddScoped<IReleaseService, ReleaseService>();
        services.AddScoped<INotificationService, NotificationService>();
        
        // Diff services
        services.AddScoped<ITextDiffEngine, TextDiffEngine>();
        services.AddScoped<IAdvancedDiffEngine, AdvancedDiffEngine>();
        services.AddScoped<ISyntaxHighlightingService, SyntaxHighlightingService>();
        services.AddScoped<IDiffCacheService, DiffCacheService>();
        services.AddScoped<IPerformanceMonitorService, PerformanceMonitorService>();
        
        // 最適化されたサービス（標準サービスを置き換え）
        services.AddScoped<IDiffViewService, OptimizedDiffViewService>();
        
        return services;
    }
    
    public static IServiceCollection AddScheduleServices(this IServiceCollection services)
    {
        // Quartz.NET設定
        services.AddQuartz(q =>
        {
            q.UseSimpleTypeLoader();
            q.UseInMemoryStore();
            q.UseDefaultThreadPool(tp =>
            {
                tp.MaxConcurrency = 10;
            });
        });
        
        services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
        
        // ISchedulerを明示的に登録
        services.AddSingleton<IScheduler>(provider =>
        {
            var schedulerFactory = provider.GetRequiredService<ISchedulerFactory>();
            return schedulerFactory.GetScheduler().GetAwaiter().GetResult();
        });
        
        // スケジュール関連サービス
        services.AddScoped<IScheduleService, ScheduleService>();
        services.AddScoped<IScheduleJobManager, ScheduleJobManager>();
        services.AddScoped<IScheduleJobExecutor, ScheduleJobExecutor>();
        
        return services;
    }
}