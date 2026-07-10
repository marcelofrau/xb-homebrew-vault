using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using XBVault.Models;

namespace XBVault.Services;

public class InspectorConsoleColorizer : DocumentColorizingTransformer
{
    private readonly IReadOnlyList<InspectorConsoleEntry> _entries;
    private static readonly IBrush MatchBg = new SolidColorBrush(Color.FromArgb(50, 154, 202, 60));

    public InspectorConsoleColorizer(IReadOnlyList<InspectorConsoleEntry> entries)
    {
        _entries = entries;
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        var idx = line.LineNumber - 1;
        if (idx < 0 || idx >= _entries.Count) return;

        var entry = _entries[idx];

        if (entry.Foreground is not null)
        {
            ChangeLinePart(line.Offset, line.EndOffset, element =>
                element.TextRunProperties.SetForegroundBrush(entry.Foreground));
        }

        if (entry.IsMatch)
        {
            ChangeLinePart(line.Offset, line.EndOffset, element =>
                element.TextRunProperties.SetBackgroundBrush(MatchBg));
        }
    }
}
