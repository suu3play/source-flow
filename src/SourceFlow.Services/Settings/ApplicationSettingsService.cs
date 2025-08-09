using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SourceFlow.Core.Models;

namespace SourceFlow.Services.Settings;

public interface IApplicationSettingsService
{
    Task<ApplicationSettings> LoadSettingsAsync();
    Task<bool> SaveSettingsAsync(ApplicationSettings settings);
    Task<bool> ResetToDefaultAsync();
    string GetSettingsFilePath();
}

public class ApplicationSettingsService : IApplicationSettingsService
{
    private readonly ILogger<ApplicationSettingsService> _logger;
    private readonly string _settingsFilePath;
    private ApplicationSettings? _cachedSettings;

    public ApplicationSettingsService(ILogger<ApplicationSettingsService> logger)
    {
        _logger = logger;
        
        // 設定ファイルのパスを決定（アプリケーションデータディレクトリ内）
        var appDataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings");
        Directory.CreateDirectory(appDataDir);
        _settingsFilePath = Path.Combine(appDataDir, "appsettings.json");
    }

    public async Task<ApplicationSettings> LoadSettingsAsync()
    {
        try
        {
            if (_cachedSettings != null)
            {
                return _cachedSettings;
            }

            if (!File.Exists(_settingsFilePath))
            {
                _logger.LogInformation("設定ファイルが存在しないため、デフォルト設定を作成します: {FilePath}", _settingsFilePath);
                _cachedSettings = CreateDefaultSettings();
                await SaveSettingsAsync(_cachedSettings);
                return _cachedSettings;
            }

            var jsonContent = await File.ReadAllTextAsync(_settingsFilePath);
            
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                AllowTrailingCommas = true
            };

            _cachedSettings = JsonSerializer.Deserialize<ApplicationSettings>(jsonContent, options);
            
            if (_cachedSettings == null)
            {
                _logger.LogWarning("設定ファイルの読み込みに失敗しました。デフォルト設定を使用します。");
                _cachedSettings = CreateDefaultSettings();
            }

            _logger.LogInformation("設定ファイルを読み込みました: {FilePath}", _settingsFilePath);
            return _cachedSettings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "設定ファイルの読み込み中にエラーが発生しました: {FilePath}", _settingsFilePath);
            _cachedSettings = CreateDefaultSettings();
            return _cachedSettings;
        }
    }

    public async Task<bool> SaveSettingsAsync(ApplicationSettings settings)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };

            var jsonContent = JsonSerializer.Serialize(settings, options);
            
            // ディレクトリが存在しない場合は作成
            var directory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(_settingsFilePath, jsonContent);
            
            // キャッシュを更新
            _cachedSettings = settings;
            
            _logger.LogInformation("設定を保存しました: {FilePath}", _settingsFilePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "設定の保存中にエラーが発生しました: {FilePath}", _settingsFilePath);
            return false;
        }
    }

    public async Task<bool> ResetToDefaultAsync()
    {
        try
        {
            var defaultSettings = CreateDefaultSettings();
            var result = await SaveSettingsAsync(defaultSettings);
            
            if (result)
            {
                _logger.LogInformation("設定をデフォルトにリセットしました");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "設定のリセット中にエラーが発生しました");
            return false;
        }
    }

    public string GetSettingsFilePath()
    {
        return _settingsFilePath;
    }

    private ApplicationSettings CreateDefaultSettings()
    {
        var settings = new ApplicationSettings();
        
        // デフォルトの作業ディレクトリを設定
        settings.General.DefaultWorkingDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
            "SourceFlow"
        );
        
        // デフォルトのログファイルパスを設定
        settings.Logging.LogFilePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, 
            "logs", 
            "application.log"
        );

        return settings;
    }
}