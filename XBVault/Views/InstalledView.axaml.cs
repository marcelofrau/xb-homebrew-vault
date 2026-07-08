using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using XBVault.Models;
using XBVault.Services;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class InstalledView : UserControl
{
    private DispatcherTimer? _spinTimer;
    private double _angle;
    private DispatcherTimer? _scrollDebounce;

    public InstalledView()
    {
        InitializeComponent();
        Loaded += (_, _) => StartSpin();
        Unloaded += (_, _) => StopSpin();
        PackageScrollViewer.ScrollChanged += OnPackageScrollChanged;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        DragDrop.AddDragOverHandler(DropPanel, OnDragOver);
        DragDrop.AddDragLeaveHandler(DropPanel, OnDragLeave);
        DragDrop.AddDropHandler(DropPanel, OnDrop);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        DragDrop.RemoveDragOverHandler(DropPanel, OnDragOver);
        DragDrop.RemoveDragLeaveHandler(DropPanel, OnDragLeave);
        DragDrop.RemoveDropHandler(DropPanel, OnDrop);
    }

    private static readonly HashSet<string> _packageExts = [".appx", ".msix", ".appxbundle", ".zip"];

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        Logger.Trace($"InstDragOver: hasFile={e.DataTransfer.Formats.Contains(DataFormat.File)}");

        if (!e.DataTransfer.Formats.Contains(DataFormat.File))
            return;

        var files = e.DataTransfer.TryGetFiles();
        Logger.Trace($"InstDragOver: files={files?.Length ?? -1}");

        if (files is null || files.Length != 1)
        {
            e.DragEffects = DragDropEffects.None;
            DropOverlay.IsVisible = false;
            return;
        }

        var ext = Path.GetExtension(files[0].Name).ToLowerInvariant();
        Logger.Trace($"InstDragOver: file={files[0].Name} ext={ext}");

        if (_packageExts.Contains(ext))
        {
            Logger.Trace($"InstDragOver: VALID — showing overlay");
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
        Logger.Trace("InstDragLeave: hiding overlay");
        DropOverlay.IsVisible = false;
    }

    private Window? GetWindow() =>
        TopLevel.GetTopLevel(this) as Window;

    private async Task ShowUnsupportedDialog(Window owner)
    {
        var dlg = new ErrorDialog(
            "Unsupported File",
            "The dropped file is not a supported package format.",
            "Supported formats: .appx, .msix, .appxbundle, .zip",
            ErrorDialogType.Warn);
        await dlg.ShowDialog(owner);
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
        {
            var win = GetWindow();
            if (win is not null)
                await ShowUnsupportedDialog(win);
            return;
        }

        if (DataContext is InstalledViewModel vm && vm.OpenCustomInstallWithFileAction is not null)
            await vm.OpenCustomInstallWithFileAction(path);
    }

    private void OnItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is StyledElement { DataContext: InstalledPackage pkg })
        {
            if (DataContext is InstalledViewModel vm)
                vm.SelectedPackage = pkg;
        }
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
        if (CatalogSpinnerImage?.RenderTransform is RotateTransform catRt)
            catRt.Angle = _angle;
    }

    private void OnPackageScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        RenderOptions.SetBitmapInterpolationMode(PackageScrollViewer, BitmapInterpolationMode.LowQuality);

        if (_scrollDebounce is null)
        {
            _scrollDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _scrollDebounce.Tick += OnScrollDebounceTick;
        }
        else
        {
            _scrollDebounce.Stop();
        }
        _scrollDebounce.Start();
    }

    private void OnScrollDebounceTick(object? sender, EventArgs e)
    {
        if (_scrollDebounce is not null)
        {
            _scrollDebounce.Tick -= OnScrollDebounceTick;
            _scrollDebounce.Stop();
            _scrollDebounce = null;
        }
        RenderOptions.SetBitmapInterpolationMode(PackageScrollViewer, BitmapInterpolationMode.HighQuality);
    }
}
