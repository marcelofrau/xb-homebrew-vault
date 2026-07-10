using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class ConfirmWindow : Window
{
    public ConfirmWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ConfirmViewModel vm)
        {
            vm.Completed += OnCompleted;
            if (vm.IsDestructive)
                ConfirmBtn.Classes.Add("Danger");
        }
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        ConfirmBtn?.Focus();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (DataContext is ConfirmViewModel vm)
                vm.CancelCommand.Execute(null);
            Close();
            e.Handled = true;
        }
    }

    private void OnCompleted(bool success)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() => Close());
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ConfirmViewModel vm)
            vm.CancelCommand.Execute(null);
        Close();
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}
