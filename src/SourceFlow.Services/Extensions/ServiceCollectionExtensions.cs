using Microsoft.Extensions.DependencyInjection;
using SourceFlow.Core.Interfaces;
using SourceFlow.Services.Configuration;
using SourceFlow.Services.Sync;
using SourceFlow.Services.Comparison;
using SourceFlow.Services.Database;
using SourceFlow.Services.Settings;
using SourceFlow.Services.Release;

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
        
        return services;
    }
}