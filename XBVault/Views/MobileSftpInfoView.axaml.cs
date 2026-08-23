#nullable enable
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;

namespace XBVault.Views;

public partial class MobileSftpInfoViewModel : ObservableObject
{
    [ObservableProperty] private string _host = "";
    [ObservableProperty] private string _user = "";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private int _port;
    public string PortDisplay => Port.ToString(CultureInfo.InvariantCulture);
}

public partial class MobileSftpInfoView : UserControl
{
    public event EventHandler? BackRequested;

    public MobileSftpInfoView()
    {
        InitializeComponent();
        var titleBar = this.FindControl<MobileTitleBar>("TitleBar");
        if (titleBar is not null)
            titleBar.BackClicked += (_, _) => BackRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }
}
