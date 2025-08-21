using SourceFlow.Core.Interfaces;
using SourceFlow.Core.Models;
using NLog;

namespace SourceFlow.Services.Diff;

public class SyntaxHighlightingService : ISyntaxHighlightingService
{
    private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
    private readonly Dictionary<string, SyntaxHighlightingConfig> _builtInConfigs;

    public SyntaxHighlightingService()
    {
        _builtInConfigs = InitializeBuiltInConfigs();
    }

    public async Task<string> DetectSyntaxFromFileAsync(string filePath)
    {
        try
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            
            await Task.CompletedTask; // 非同期メソッドの形式を維持
            
            return extension switch
            {
                ".cs" => "C#",
                ".xml" => "XML",
                ".json" => "JSON",
                ".js" => "JavaScript",
                ".ts" => "TypeScript",
                ".html" => "HTML",
                ".css" => "CSS",
                ".sql" => "SQL",
                ".py" => "Python",
                ".java" => "Java",
                ".cpp" or ".cxx" or ".cc" => "C++",
                ".c" or ".h" => "C",
                ".php" => "PHP",
                ".rb" => "Ruby",
                ".go" => "Go",
                ".rs" => "Rust",
                ".md" => "Markdown",
                ".yml" or ".yaml" => "YAML",
                ".ini" => "INI",
                ".bat" or ".cmd" => "Batch",
                ".ps1" => "PowerShell",
                _ => "Text"
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "ファイルからのシンタックス検出に失敗: {FilePath}", filePath);
            return "Text";
        }
    }

    public async Task<string> DetectSyntaxFromContentAsync(string content, string fileName = "")
    {
        try
        {
            // ファイル名が指定されている場合、まず拡張子で判定
            if (!string.IsNullOrEmpty(fileName))
            {
                var syntaxFromFile = await DetectSyntaxFromFileAsync(fileName);
                if (syntaxFromFile != "Text")
                    return syntaxFromFile;
            }

            await Task.CompletedTask; // 非同期メソッドの形式を維持

            // コンテンツの最初の数行を分析して形式を推測
            var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                              .Take(10)
                              .ToArray();

            // XML/HTML
            if (lines.Any(line => line.TrimStart().StartsWith("<?xml") || 
                                 line.TrimStart().StartsWith("<!DOCTYPE html")))
                return "XML";

            // JSON
            if (content.TrimStart().StartsWith("{") || content.TrimStart().StartsWith("["))
                return "JSON";

            // C#
            if (lines.Any(line => line.Contains("using ") || 
                                 line.Contains("namespace ") ||
                                 line.Contains("class ") ||
                                 line.Contains("public ")))
                return "C#";

            // SQL
            if (lines.Any(line => line.ToUpperInvariant().Contains("SELECT ") ||
                                 line.ToUpperInvariant().Contains("FROM ") ||
                                 line.ToUpperInvariant().Contains("CREATE TABLE")))
                return "SQL";

            return "Text";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "コンテンツからのシンタックス検出に失敗");
            return "Text";
        }
    }

    public async Task<SyntaxHighlightingConfig> GetHighlightingConfigAsync(string syntaxName)
    {
        try
        {
            await Task.CompletedTask; // 非同期メソッドの形式を維持
            
            if (_builtInConfigs.TryGetValue(syntaxName, out var config))
                return config;

            return _builtInConfigs["Text"];
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "シンタックスハイライト設定の取得に失敗: {SyntaxName}", syntaxName);
            return _builtInConfigs["Text"];
        }
    }

    public async Task<List<SyntaxHighlightingConfig>> GetAvailableHighlightingConfigsAsync()
    {
        try
        {
            await Task.CompletedTask; // 非同期メソッドの形式を維持
            return _builtInConfigs.Values.ToList();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "利用可能なシンタックスハイライト設定の取得に失敗");
            return [];
        }
    }

    public async Task<bool> RegisterCustomHighlightingAsync(SyntaxHighlightingConfig config)
    {
        try
        {
            await Task.CompletedTask; // 非同期メソッドの形式を維持
            
            if (string.IsNullOrEmpty(config.Name))
                return false;

            _builtInConfigs[config.Name] = config;
            _logger.Info("カスタムシンタックスハイライト設定を登録: {Name}", config.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "カスタムシンタックスハイライト設定の登録に失敗: {Name}", config.Name);
            return false;
        }
    }

    private static Dictionary<string, SyntaxHighlightingConfig> InitializeBuiltInConfigs()
    {
        return new Dictionary<string, SyntaxHighlightingConfig>
        {
            ["C#"] = new SyntaxHighlightingConfig
            {
                Name = "C#",
                FileExtension = ".cs",
                XshdResource = "C#",
                IsBuiltIn = true,
                ColorScheme = new Dictionary<string, string>
                {
                    ["Keyword"] = "#569CD6",
                    ["String"] = "#D69E2E",
                    ["Comment"] = "#57A64A",
                    ["Number"] = "#B5CEA8"
                }
            },
            ["XML"] = new SyntaxHighlightingConfig
            {
                Name = "XML",
                FileExtension = ".xml",
                XshdResource = "XML",
                IsBuiltIn = true,
                ColorScheme = new Dictionary<string, string>
                {
                    ["Tag"] = "#569CD6",
                    ["Attribute"] = "#92CAF4",
                    ["String"] = "#D69E2E"
                }
            },
            ["JSON"] = new SyntaxHighlightingConfig
            {
                Name = "JSON",
                FileExtension = ".json",
                XshdResource = "JSON",
                IsBuiltIn = true,
                ColorScheme = new Dictionary<string, string>
                {
                    ["Property"] = "#92CAF4",
                    ["String"] = "#D69E2E",
                    ["Number"] = "#B5CEA8"
                }
            },
            ["Text"] = new SyntaxHighlightingConfig
            {
                Name = "Text",
                FileExtension = ".txt",
                XshdResource = "",
                IsBuiltIn = true,
                ColorScheme = new Dictionary<string, string>()
            }
        };
    }
}