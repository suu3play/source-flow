using SourceFlow.Core.Models;

namespace SourceFlow.Core.Interfaces;

public interface IConfigurationService
{
    Task<List<SourceConfiguration>> GetSourceConfigurationsAsync();
    Task SaveSourceConfigurationsAsync(List<SourceConfiguration> configurations);
    Task<T> GetSettingAsync<T>(string key, T defaultValue);
    Task SaveSettingAsync<T>(string key, T value);
}