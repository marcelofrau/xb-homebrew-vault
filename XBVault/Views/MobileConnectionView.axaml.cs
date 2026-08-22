using System;
using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using XBVault.Helpers;
using XBVault.Services;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class MobileConnectionView : UserControl
{
    private Action? _onBack;

    public MobileConnectionView()
    {
        InitializeComponent();
        TitleBar.BackClicked += (_, _) => _onBack?.Invoke();
        DataContextChanged += OnDataContextChanged;
    }

    public void SetOnBack(Action onBack) => _onBack = onBack;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ConnectionViewModel vm)
        {
            vm.OutputLines.CollectionChanged += OnOutputLinesChanged;
            vm.Completed += OnConnectionCompleted;
            vm.CloseAction = () => _onBack?.Invoke();
        }
    }

    private void OnConnectionCompleted(bool success)
    {
        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(success ? 2000 : 500).ConfigureAwait(false);
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (!success && DataContext is ConnectionViewModel vm)
                    {
                        var lines = vm.OutputLines;
                        var detail = lines.Count > 0 ? string.Join("\n", lines.TakeLast(5)) : "Connection failed";
                        var msg = detail.Contains("PASSWORD REJECTED") || detail.Contains("ACCESS DENIED")
                            ? "Check your credentials in Settings and try again."
                            : detail.Contains("BUSY SIGNAL") || detail.Contains("NO CARRIER") || detail.Contains("NO ANSWER")
                                ? "Xbox may be off or unreachable. Verify it's powered on and on the same network."
                                : "Verify your Xbox connection settings and try again.";

                        var errorType = detail.Contains("PASSWORD REJECTED") || detail.Contains("ACCESS DENIED")
                            ? ErrorDialogType.Warn
                            : ErrorDialogType.Error;

                        var dlg = new MobileErrorDialogView
                        {
                            DataContext = new MobileErrorDialogViewModel
                            {
                                Title = "Connection Failed",
                                Description = msg,
                                Details = detail,
                                DialogType = errorType
                            }
                        };
                        dlg.OkClicked += (_, _) =>
                        {
                            RemoveDialogFromPanel(dlg);
                            _onBack?.Invoke();
                        };
                        var topLevel = TopLevel.GetTopLevel(this);
                        if (topLevel?.Content is Panel panel)
                            panel.Children.Add(dlg);
                    }
                    else
                    {
                        _onBack?.Invoke();
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "MobileConnectionView: delayed close failed");
            }
        }).FireAndForget();
    }

    private static void RemoveDialogFromPanel(UserControl dlg)
    {
        var topLevel = TopLevel.GetTopLevel(dlg);
        if (topLevel?.Content is Panel panel)
            panel.Children.Remove(dlg);
    }

    private void OnOutputLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
            OutputScroll?.ScrollToEnd();
    }
}
