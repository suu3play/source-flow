using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using SourceFlow.Core.Models;
using SourceFlow.Core.Enums;
using System.Windows;
using System.Windows.Media;

namespace SourceFlow.UI.Highlighting;

public class DiffLineTransformation : DocumentColorizingTransformer
{
    private readonly List<LineDiff> _lineDiffs;
    private readonly DiffColorScheme _colorScheme;
    
    public DiffLineTransformation(List<LineDiff> lineDiffs, DiffColorScheme? colorScheme = null)
    {
        _lineDiffs = lineDiffs ?? [];
        _colorScheme = colorScheme ?? DiffColorScheme.Default;
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        if (line.LineNumber <= _lineDiffs.Count)
        {
            var lineDiff = _lineDiffs[line.LineNumber - 1];
            var brush = GetBackgroundBrush(lineDiff.ChangeType);
            var foregroundBrush = GetForegroundBrush(lineDiff.ChangeType);

            if (brush != null || foregroundBrush != null)
            {
                ChangeLinePart(line.Offset, line.EndOffset, element =>
                {
                    if (brush != null)
                    {
                        element.BackgroundBrush = brush;
                    }
                    if (foregroundBrush != null)
                    {
                        element.TextRunProperties.SetForegroundBrush(foregroundBrush);
                    }
                    if (lineDiff.ChangeType != ChangeType.NoChange)
                    {
                        element.TextRunProperties.SetTypeface(new Typeface(
                            element.TextRunProperties.Typeface.FontFamily,
                            element.TextRunProperties.Typeface.Style,
                            FontWeights.Medium,
                            element.TextRunProperties.Typeface.Stretch));
                    }
                });
            }

            // 文字レベルの差分ハイライト
            if (lineDiff.CharacterDiffs?.Any() == true)
            {
                ApplyCharacterDiffs(line, lineDiff.CharacterDiffs);
            }
        }
    }

    private void ApplyCharacterDiffs(DocumentLine line, List<CharacterDiff> characterDiffs)
    {
        foreach (var charDiff in characterDiffs)
        {
            if (charDiff.StartIndex >= 0 && charDiff.Length > 0)
            {
                var start = line.Offset + charDiff.StartIndex;
                var end = Math.Min(start + charDiff.Length, line.EndOffset);
                
                if (start < end)
                {
                    var brush = GetCharacterDiffBrush(charDiff.ChangeType);
                    if (brush != null)
                    {
                        ChangeLinePart(start, end, element =>
                        {
                            element.BackgroundBrush = brush;
                            element.TextRunProperties.SetTypeface(new Typeface(
                                element.TextRunProperties.Typeface.FontFamily,
                                element.TextRunProperties.Typeface.Style,
                                FontWeights.Bold,
                                element.TextRunProperties.Typeface.Stretch));
                        });
                    }
                }
            }
        }
    }

    private SolidColorBrush? GetBackgroundBrush(ChangeType changeType)
    {
        return changeType switch
        {
            ChangeType.Add => _colorScheme.AddedBackground,
            ChangeType.Delete => _colorScheme.DeletedBackground,
            ChangeType.Modify => _colorScheme.ModifiedBackground,
            _ => null
        };
    }

    private SolidColorBrush? GetForegroundBrush(ChangeType changeType)
    {
        return changeType switch
        {
            ChangeType.Add => _colorScheme.AddedForeground,
            ChangeType.Delete => _colorScheme.DeletedForeground,
            ChangeType.Modify => _colorScheme.ModifiedForeground,
            _ => _colorScheme.NormalForeground
        };
    }

    private SolidColorBrush? GetCharacterDiffBrush(ChangeType changeType)
    {
        return changeType switch
        {
            ChangeType.Add => _colorScheme.CharacterAddedBackground,
            ChangeType.Delete => _colorScheme.CharacterDeletedBackground,
            ChangeType.Modify => _colorScheme.CharacterModifiedBackground,
            _ => null
        };
    }
}

public class DiffColorScheme
{
    public SolidColorBrush? AddedBackground { get; set; }
    public SolidColorBrush? DeletedBackground { get; set; }
    public SolidColorBrush? ModifiedBackground { get; set; }
    public SolidColorBrush? AddedForeground { get; set; }
    public SolidColorBrush? DeletedForeground { get; set; }
    public SolidColorBrush? ModifiedForeground { get; set; }
    public SolidColorBrush? NormalForeground { get; set; }
    public SolidColorBrush? CharacterAddedBackground { get; set; }
    public SolidColorBrush? CharacterDeletedBackground { get; set; }
    public SolidColorBrush? CharacterModifiedBackground { get; set; }

    public static DiffColorScheme Default => new()
    {
        AddedBackground = new SolidColorBrush(Color.FromArgb(80, 40, 167, 69)),      // 薄い緑
        DeletedBackground = new SolidColorBrush(Color.FromArgb(80, 220, 53, 69)),    // 薄い赤
        ModifiedBackground = new SolidColorBrush(Color.FromArgb(80, 255, 193, 7)),   // 薄い黄
        AddedForeground = new SolidColorBrush(Color.FromRgb(21, 87, 36)),            // 濃い緑
        DeletedForeground = new SolidColorBrush(Color.FromRgb(114, 28, 36)),         // 濃い赤
        ModifiedForeground = new SolidColorBrush(Color.FromRgb(102, 77, 3)),         // 濃い黄
        NormalForeground = new SolidColorBrush(Color.FromRgb(33, 37, 41)),           // 標準色
        CharacterAddedBackground = new SolidColorBrush(Color.FromArgb(120, 40, 167, 69)),
        CharacterDeletedBackground = new SolidColorBrush(Color.FromArgb(120, 220, 53, 69)),
        CharacterModifiedBackground = new SolidColorBrush(Color.FromArgb(120, 255, 193, 7))
    };

    public static DiffColorScheme Dark => new()
    {
        AddedBackground = new SolidColorBrush(Color.FromArgb(80, 46, 160, 67)),
        DeletedBackground = new SolidColorBrush(Color.FromArgb(80, 248, 81, 73)),
        ModifiedBackground = new SolidColorBrush(Color.FromArgb(80, 255, 221, 87)),
        AddedForeground = new SolidColorBrush(Color.FromRgb(154, 255, 154)),
        DeletedForeground = new SolidColorBrush(Color.FromRgb(255, 154, 154)),
        ModifiedForeground = new SolidColorBrush(Color.FromRgb(255, 255, 154)),
        NormalForeground = new SolidColorBrush(Color.FromRgb(248, 248, 242)),
        CharacterAddedBackground = new SolidColorBrush(Color.FromArgb(150, 46, 160, 67)),
        CharacterDeletedBackground = new SolidColorBrush(Color.FromArgb(150, 248, 81, 73)),
        CharacterModifiedBackground = new SolidColorBrush(Color.FromArgb(150, 255, 221, 87))
    };
}