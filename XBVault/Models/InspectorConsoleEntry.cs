using Avalonia.Media;

namespace XBVault.Models;

public class InspectorConsoleEntry
{
    public string Text { get; set; } = "";
    public IBrush? Foreground { get; set; }
    public bool IsMatch { get; set; }
}
