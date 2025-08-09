using System.Text.Json;
using SourceFlow.Core.Interfaces;
using SourceFlow.Core.Models;
using NLog;

namespace SourceFlow.Services.Configuration;

public class ConfigurationService : IConfigurationService
{
    private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
    private readonly string _configDirectory;
    
    public ConfigurationService()
    {
        _configDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config");
        if (!Directory.Exists(_configDirectory))
        {
            Directory.CreateDirectory(_configDirectory);
        }
    }
    
    public async Task<List<SourceConfiguration>> GetSourceConfigurationsAsync()
    {
        try
        {
            var filePath = Path.Combine(_configDirectory, "source_sync.json");
            if (!File.Exists(filePath))
            {
                _logger.Info("設定ファイルが存在しません。デフォルト設定を作成します: {FilePath}", filePath);
                return new List<SourceConfiguration>();
            }
            
            var jsonContent = await File.ReadAllTextAsync(filePath);
            var config = JsonSerializer.Deserialize<SourceSyncConfig>(jsonContent);
            
            return config?.Sources ?? new List<SourceConfiguration>();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "ソース設定の読み込みに失敗しました");
            return new List<SourceConfiguration>();
        }
    }
    
    public async Task SaveSourceConfigurationsAsync(List<SourceConfiguration> configurations)
    {
        try
        {
            var config = new SourceSyncConfig
            {
                Sources = configurations,
                SyncOptions = new SyncOptions()
            };
            
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var jsonContent = JsonSerializer.Serialize(config, options);
            var filePath = Path.Combine(_configDirectory, "source_sync.json");
            
            await File.WriteAllTextAsync(filePath, jsonContent);
            _logger.Info("ソース設定を保存しました: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "ソース設定の保存に失敗しました");
            throw;
        }
    }
    
    public async Task<T> GetSettingAsync<T>(string key, T defaultValue)
    {
        try
        {
            var filePath = Path.Combine(_configDirectory, "app_config.json");
            if (!File.Exists(filePath))
            {
                return defaultValue;
            }
            
            var jsonContent = await File.ReadAllTextAsync(filePath);
            var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonContent);
            
            if (config != null && config.TryGetValue(key, out var value))
            {
                return value.Deserialize<T>() ?? defaultValue;
            }
            
            return defaultValue;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "設定値の取得に失敗しました: {Key}", key);
            return defaultValue;
        }
    }
    
    public async Task SaveSettingAsync<T>(string key, T value)
    {
        try
        {
            var filePath = Path.Combine(_configDirectory, "app_config.json");
            var config = new Dictionary<string, object>();
            
            if (File.Exists(filePath))
            {
                var jsonContent = await File.ReadAllTextAsync(filePath);
                var existingConfig = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonContent);
                if (existingConfig != null)
                {
                    config = existingConfig;
                }
            }
            
            config[key] = value!;
            
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var newJsonContent = JsonSerializer.Serialize(config, options);
            await File.WriteAllTextAsync(filePath, newJsonContent);
            
            _logger.Info("設定値を保存しました: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "設定値の保存に失敗しました: {Key}", key);
            throw;
        }
    }
    
    private class SourceSyncConfig
    {
        public List<SourceConfiguration> Sources { get; set; } = new();
        public SyncOptions SyncOptions { get; set; } = new();
    }
    
    private class SyncOptions
    {
        public int TimeoutSeconds { get; set; } = 300;
        public int RetryCount { get; set; } = 3;
        public bool PreservePermissions { get; set; } = false;
        public bool VerifyChecksum { get; set; } = true;
        public int LocalAccessTimeout { get; set; } = 30;
    }
}