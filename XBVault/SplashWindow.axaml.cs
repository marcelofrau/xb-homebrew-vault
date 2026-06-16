using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.Threading.Tasks;

namespace XBVault;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
    }

    public async void CloseAfterDelay()
    {
        await Task.Delay(2500);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Close();
        });
    }
}
