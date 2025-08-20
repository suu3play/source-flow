using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using SourceFlow.Core.Interfaces;
using SourceFlow.Core.Models;
using System.IO;
using System.Reflection;
using System.Xml;
using NLog;

namespace SourceFlow.UI.Services;

public class AdvancedSyntaxHighlightingService : ISyntaxHighlightingService
{
    private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
    private readonly Dictionary<string, IHighlightingDefinition> _customDefinitions;
    private readonly Dictionary<string, string> _fileExtensionMappings;

    public AdvancedSyntaxHighlightingService()
    {
        _customDefinitions = [];
        _fileExtensionMappings = [];
        InitializeCustomDefinitions();
        InitializeFileExtensionMappings();
    }

    public async Task<string> DetectSyntaxFromFileAsync(string filePath)
    {
        try
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            
            // カスタム拡張子マッピングを優先
            if (_fileExtensionMappings.TryGetValue(extension, out var customMapping))
            {
                return customMapping;
            }

            // AvalonEdit標準の検出を使用
            var definition = HighlightingManager.Instance.GetDefinitionByExtension(extension);
            if (definition != null)
            {
                return definition.Name;
            }

            // ファイル内容による検出（簡易実装）
            if (File.Exists(filePath))
            {
                var content = await File.ReadAllTextAsync(filePath);
                return DetectSyntaxFromContent(content, extension);
            }

            return "Text";
        }
        catch (Exception ex)
        {
            _logger.Warn(ex, "シンタックス検出に失敗しました: {FilePath}", filePath);
            return "Text";
        }
    }

    public async Task<string> DetectSyntaxFromContentAsync(string content, string fileName = "")
    {
        try
        {
            var extension = string.IsNullOrEmpty(fileName) ? "" : Path.GetExtension(fileName);
            return await Task.FromResult(DetectSyntaxFromContent(content, extension));
        }
        catch (Exception ex)
        {
            _logger.Warn(ex, "コンテンツからのシンタックス検出に失敗しました");
            return "Text";
        }
    }

    public IHighlightingDefinition? GetHighlightingDefinition(string name)
    {
        // カスタム定義を優先
        if (_customDefinitions.TryGetValue(name, out var customDef))
        {
            return customDef;
        }

        // 標準定義
        return HighlightingManager.Instance.GetDefinition(name);
    }

    public IEnumerable<string> GetAvailableSyntaxNames()
    {
        var standardNames = HighlightingManager.Instance.HighlightingDefinitions
            .Select(d => d.Name);
        var customNames = _customDefinitions.Keys;
        
        return standardNames.Concat(customNames).Distinct().OrderBy(n => n);
    }

    public void RegisterCustomDefinition(string name, string xshdContent)
    {
        try
        {
            using var reader = new StringReader(xshdContent);
            using var xmlReader = XmlReader.Create(reader);
            var definition = HighlightingLoader.Load(xmlReader, HighlightingManager.Instance);
            
            _customDefinitions[name] = definition;
            _logger.Info("カスタム定義を登録しました: {Name}", name);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "カスタム定義の登録に失敗しました: {Name}", name);
        }
    }

    public async Task<SyntaxHighlightingConfig> GetHighlightingConfigAsync(string syntaxName)
    {
        var definition = GetHighlightingDefinition(syntaxName);
        return await Task.FromResult(new SyntaxHighlightingConfig
        {
            Name = syntaxName,
            FileExtension = syntaxName.ToLowerInvariant(),
            XshdResource = definition?.Name ?? "Text",
            ColorScheme = new Dictionary<string, string>(),
            IsBuiltIn = true
        });
    }

    public async Task<List<SyntaxHighlightingConfig>> GetAvailableHighlightingConfigsAsync()
    {
        var configs = new List<SyntaxHighlightingConfig>();
        
        foreach (var name in GetAvailableSyntaxNames())
        {
            var config = await GetHighlightingConfigAsync(name);
            configs.Add(config);
        }
        
        return configs;
    }

    public async Task<bool> RegisterCustomHighlightingAsync(SyntaxHighlightingConfig config)
    {
        try
        {
            if (!string.IsNullOrEmpty(config.XshdResource))
            {
                RegisterCustomDefinition(config.Name, config.XshdResource);
            }
            
            if (!string.IsNullOrEmpty(config.FileExtension))
            {
                AddFileExtensionMapping(config.FileExtension, config.Name);
            }
            
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "カスタムハイライト設定の登録に失敗: {ConfigName}", config.Name);
            return await Task.FromResult(false);
        }
    }

    public void AddFileExtensionMapping(string extension, string syntaxName)
    {
        _fileExtensionMappings[extension.ToLowerInvariant()] = syntaxName;
        _logger.Debug("ファイル拡張子マッピング追加: {Extension} -> {SyntaxName}", extension, syntaxName);
    }

    private void InitializeCustomDefinitions()
    {
        try
        {
            // Diff定義をリソースから読み込み
            var assembly = Assembly.GetExecutingAssembly();
            var diffResourceName = "SourceFlow.UI.Highlighting.DiffHighlighting.xshd";
            
            using var stream = assembly.GetManifestResourceStream(diffResourceName);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                var xshdContent = reader.ReadToEnd();
                RegisterCustomDefinition("Diff", xshdContent);
            }
            else
            {
                // ファイルシステムからフォールバック読み込み
                var diffFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                    "Highlighting", "DiffHighlighting.xshd");
                
                if (File.Exists(diffFilePath))
                {
                    var xshdContent = File.ReadAllText(diffFilePath);
                    RegisterCustomDefinition("Diff", xshdContent);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warn(ex, "カスタム定義の初期化に失敗しました");
        }
    }

    private void InitializeFileExtensionMappings()
    {
        // 追加の拡張子マッピング
        var mappings = new Dictionary<string, string>
        {
            { ".tsx", "TypeScript" },
            { ".jsx", "JavaScript" },
            { ".vue", "XML" },
            { ".svelte", "XML" },
            { ".razor", "XML" },
            { ".cshtml", "XML" },
            { ".vbhtml", "XML" },
            { ".ps1", "PowerShell" },
            { ".psm1", "PowerShell" },
            { ".psd1", "PowerShell" },
            { ".dockerfile", "Dockerfile" },
            { ".gitignore", "Text" },
            { ".gitattributes", "Text" },
            { ".editorconfig", "Text" },
            { ".diff", "Diff" },
            { ".patch", "Diff" }
        };

        foreach (var (extension, syntax) in mappings)
        {
            _fileExtensionMappings[extension] = syntax;
        }
    }

    private string DetectSyntaxFromContent(string content, string extension = "")
    {
        // 内容ベースの簡易検出ロジック
        var trimmedContent = content.TrimStart();
        
        // Shebang検出
        if (trimmedContent.StartsWith("#!"))
        {
            var firstLine = trimmedContent.Split('\n')[0].ToLowerInvariant();
            if (firstLine.Contains("python")) return "Python";
            if (firstLine.Contains("bash") || firstLine.Contains("sh")) return "Bash";
            if (firstLine.Contains("powershell") || firstLine.Contains("pwsh")) return "PowerShell";
            if (firstLine.Contains("node")) return "JavaScript";
        }

        // XML/HTML検出
        if (trimmedContent.StartsWith("<?xml") || trimmedContent.StartsWith("<!DOCTYPE"))
        {
            return "XML";
        }

        // JSON検出
        if ((trimmedContent.StartsWith("{") || trimmedContent.StartsWith("[")) && 
            trimmedContent.TrimEnd().EndsWith("}") || trimmedContent.TrimEnd().EndsWith("]"))
        {
            try
            {
                // 簡易JSON検証
                if (trimmedContent.Contains("\"") && (trimmedContent.Contains(":") || trimmedContent.Contains(",")))
                {
                    return "JSON";
                }
            }
            catch
            {
                // JSON検証失敗時は継続
            }
        }

        // C#検出
        if (trimmedContent.Contains("using System") || 
            trimmedContent.Contains("namespace ") ||
            trimmedContent.Contains("class ") ||
            trimmedContent.Contains("public class"))
        {
            return "C#";
        }

        // 拡張子ベースのフォールバック
        return extension switch
        {
            ".md" => "MarkDown",
            ".yml" or ".yaml" => "YAML",
            ".toml" => "Text",
            ".ini" => "Text",
            ".cfg" => "Text",
            ".conf" => "Text",
            _ => "Text"
        };
    }
}