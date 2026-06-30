using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class BrowseView : UserControl
{
    private DispatcherTimer? _spinTimer;
    private double _angle;

    public BrowseView()
    {
        InitializeComponent();
        Loaded += (_, _) => StartSpin();
        Unloaded += (_, _) => StopSpin();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        DragDrop.AddDragOverHandler(PackageListBox, OnDragOver);
        DragDrop.AddDragLeaveHandler(PackageListBox, OnDragLeave);
        DragDrop.AddDropHandler(PackageListBox, OnDrop);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        DragDrop.RemoveDragOverHandler(PackageListBox, OnDragOver);
        DragDrop.RemoveDragLeaveHandler(PackageListBox, OnDragLeave);
        DragDrop.RemoveDropHandler(PackageListBox, OnDrop);
    }

    private static readonly HashSet<string> _packageExts = [".appx", ".msix", ".appxbundle", ".zip"];

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Formats.Contains(DataFormat.File))
            return;

        var files = e.DataTransfer.TryGetFiles();
        if (files is null || files.Length != 1)
            return;

        var ext = Path.GetExtension(files[0].Name).ToLowerInvariant();
        if (_packageExts.Contains(ext))
        {
            e.DragEffects = DragDropEffects.Copy;
            DropOverlay.IsVisible = true;
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
        if (files is null || files.Length != 1)
            return;

        var path = files[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(path))
            return;

        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (!_packageExts.Contains(ext))
            return;

        if (DataContext is BrowseViewModel vm && vm.OpenCustomInstallWithFileAction is not null)
            await vm.OpenCustomInstallWithFileAction(path);
    }

    private void StartSpin()
    {
        _spinTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _spinTimer.Tick += SpinTick;
        _spinTimer.Start();
    }

    private void StopSpin()
    {
        if (_spinTimer is null) return;
        _spinTimer.Tick -= SpinTick;
        _spinTimer.Stop();
        _spinTimer = null;
    }

    private void SpinTick(object? sender, EventArgs e)
    {
        _angle = (_angle - 6 + 360) % 360;
        if (SpinnerImage.RenderTransform is RotateTransform rt)
            rt.Angle = _angle;
    }
}
