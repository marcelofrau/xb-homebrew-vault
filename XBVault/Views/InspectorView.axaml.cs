#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using XBVault.Services;

namespace XBVault.Views;

public partial class InspectorView : UserControl
{
    private static readonly HashSet<string> _packageExts = [".appx", ".msix", ".appxbundle", ".msixbundle", ".zip"];
    private InspectorConsoleColorizer? _colorizer;
    private ViewModels.InspectorViewModel? _vm;

    public TextEditor? Editor => ConsoleEditor;

    public InspectorView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is ViewModels.InspectorViewModel vm)
        {
            _vm = vm;
            vm.ConsoleEntries.CollectionChanged += OnConsoleEntriesChanged;
            vm.FilterChanged += OnFilterChanged;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        DragDrop.AddDragOverHandler(DropPanel, OnDragOver);
        DragDrop.AddDragLeaveHandler(DropPanel, OnDragLeave);
        DragDrop.AddDropHandler(DropPanel, OnDrop);
        ReplInput.AddHandler(KeyDownEvent, OnReplInputKeyDown, RoutingStrategies.Tunnel);

        InitEditor();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        DragDrop.RemoveDragOverHandler(DropPanel, OnDragOver);
        DragDrop.RemoveDragLeaveHandler(DropPanel, OnDragLeave);
        DragDrop.RemoveDropHandler(DropPanel, OnDrop);
    }

    private void InitEditor()
    {
        if (_vm is null) return;

        _colorizer = new InspectorConsoleColorizer(_vm.ConsoleEntries);
        ConsoleEditor.TextArea.TextView.LineTransformers.Add(_colorizer);

        foreach (var entry in _vm.ConsoleEntries)
            ConsoleEditor.AppendText(entry.Text + "\n");

        if (_vm.AutoScroll)
            ScrollToEndDeferred();
    }

    private void ScrollToEndDeferred()
    {
        XBVault.Helpers.UIHelpers.RunOnUI(() =>
        {
            if (ConsoleEditor.Document is null) return;
            ConsoleEditor.CaretOffset = ConsoleEditor.Document.TextLength;
            ConsoleEditor.TextArea.Caret.BringCaretToView();
        }, DispatcherPriority.Render);
    }

    private void OnConsoleEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_vm is null) return;

        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems is not null:
                foreach (var item in e.NewItems)
                {
                    if (item is Models.InspectorConsoleEntry entry)
                        ConsoleEditor.AppendText(entry.Text + "\n");
                }
                if (_vm.AutoScroll)
                    ScrollToEndDeferred();
                break;
            case NotifyCollectionChangedAction.Reset:
                ConsoleEditor.Document.Text = "";
                break;
        }
    }

    private void OnFilterChanged()
    {
        if (_colorizer is not null)
            ConsoleEditor.TextArea.TextView.Redraw();
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Formats.Contains(DataFormat.File))
            return;

        var files = e.DataTransfer.TryGetFiles();
        if (files is null || files.Length != 1)
        {
            e.DragEffects = DragDropEffects.None;
            DropOverlay.IsVisible = false;
            return;
        }

        var ext = Path.GetExtension(files[0].Name).ToLowerInvariant();
        if (_packageExts.Contains(ext))
        {
            e.DragEffects = DragDropEffects.Copy;
            if (!DropOverlay.IsVisible)
                DropOverlay.IsVisible = true;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
            DropOverlay.IsVisible = false;
        }
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        DropOverlay.IsVisible = false;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        DropOverlay.IsVisible = false;

        var files = e.DataTransfer.TryGetFiles();
        if (files is null || files.Length != 1) return;

        var path = files[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;

        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (!_packageExts.Contains(ext))
        {
            var win = GetWindow();
            if (win is not null)
                await ShowUnsupportedDialog(win);
            return;
        }

        if (_vm is not null && _vm.OpenCustomInstallWithFileAction is not null)
            await _vm.OpenCustomInstallWithFileAction(path);
    }

    private Window? GetWindow() =>
        TopLevel.GetTopLevel(this) as Window;

    private static async Task ShowUnsupportedDialog(Window owner)
    {
        var dlg = new ErrorDialog(
            "Unsupported File",
            "The dropped file is not a supported package format.",
            "Supported formats: .appx, .msix, .appxbundle, .zip",
            ErrorDialogType.Warn);
        await dlg.ShowDialog(owner);
    }

    private void OnFilterKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _vm is not null)
        {
            _vm.CloseFilterCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && _vm is not null)
        {
            GoToFirstMatch();
            e.Handled = true;
        }
    }

    private void GoToFirstMatch()
    {
        if (_vm is null) return;
        for (int i = 0; i < _vm.ConsoleEntries.Count; i++)
        {
            if (_vm.ConsoleEntries[i].IsMatch)
            {
                ConsoleEditor.ScrollToLine(i + 1);
                return;
            }
        }
    }

    private void OnCopyClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ConsoleEditor.Copy();
    }

    private void OnSelectAllClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ConsoleEditor.SelectAll();
    }

    public bool ScrollConsolePage(Key key)
    {
        if (ConsoleEditor?.Document is null) return false;

        var lineCount = ConsoleEditor.Document.LineCount;
        if (lineCount == 0) return false;

        var currentLine = ConsoleEditor.TextArea.Caret.Line;
        var lineHeight = ConsoleEditor.TextArea.TextView.DefaultLineHeight;
        if (lineHeight <= 0) lineHeight = 16.0;
        var viewportLines = Math.Max((int)(ConsoleEditor.Bounds.Height / lineHeight), 1);

        int targetLine;
        switch (key)
        {
            case Key.PageDown:
                targetLine = Math.Min(currentLine + viewportLines, lineCount);
                break;
            case Key.PageUp:
                targetLine = Math.Max(currentLine - viewportLines, 1);
                break;
            case Key.Home:
                targetLine = 1;
                break;
            case Key.End:
                targetLine = lineCount;
                break;
            case Key.Down:
                targetLine = Math.Min(currentLine + 1, lineCount);
                break;
            case Key.Up:
                targetLine = Math.Max(currentLine - 1, 1);
                break;
            default:
                return false;
        }

        ConsoleEditor.ScrollToLine(targetLine);
        return true;
    }

    private void OnReplInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (_vm is not null)
                _vm.SendCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnXrayDepotLinkClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://github.com/marcelofrau/uwp-xray-depot") { UseShellExecute = true });
    }

    private void OnPyConnectorLinkClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://github.com/marcelofrau/xb-xray-py-connector") { UseShellExecute = true });
    }
}
