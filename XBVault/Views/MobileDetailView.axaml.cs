#nullable enable
using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using XBVault.Helpers;
using XBVault.Models;
using XBVault.Services;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class MobileDetailView : UserControl
{
    private Action? _onBack;

    public MobileDetailView()
    {
        InitializeComponent();
        TitleBar.BackClicked += (_, _) => _onBack?.Invoke();
    }

    public void SetOnBack(Action onBack) => _onBack = onBack;

    private void OnBackClick(object? sender, RoutedEventArgs e) => _onBack?.Invoke();

    private void OnFinishClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BrowseViewModel vm)
            vm.CloseDetailCommand.Execute(null);
        _onBack?.Invoke();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BrowseViewModel vm)
            vm.CloseDetailCommand.Execute(null);
        _onBack?.Invoke();
    }

    private void OnInstallClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (DataContext is not BrowseViewModel vm) return;
        if (vm.SelectedItem is null) return;

        var installable = vm.SelectedItem.Downloads
            .Where(d => d.DownloadType == DownloadType.MainPackage || d.DownloadType == DownloadType.Unknown)
            .ToList();

        if (installable.Count > 1)
        {
            var flyout = new MenuFlyout();
            foreach (var download in installable)
            {
                var item = new MenuItem
                {
                    Header = new TextBlock { Text = download.Label ?? download.Url, Foreground = Brushes.White }
                };
                var captured = download;
                item.Click += async (_, _) => await vm.InstallByAssetAsync(captured);
                flyout.Items.Add(item);
            }
            flyout.ShowAt(btn);
        }
        else
        {
            vm.InstallSelectedCommand.Execute(null);
        }
    }

    private void OnDeveloperClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var vm = DataContext as BrowseViewModel;
        var contributor = vm?.SelectedItem?.Contributors
            .FirstOrDefault(c => c.Role == "Developer" && c.Name == vm!.SelectedItem!.Developer);
        if (contributor is not null)
            ShowContributorFlyout(btn, contributor);
    }

    private void OnUwpPortByClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var vm = DataContext as BrowseViewModel;
        var contributor = vm?.SelectedItem?.Contributors
            .FirstOrDefault(c => c.Role == "Porter" && c.Name == vm!.SelectedItem!.UwpPortBy);
        if (contributor is not null)
            ShowContributorFlyout(btn, contributor);
    }

    private void OnMaintainedByClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var vm = DataContext as BrowseViewModel;
        var contributor = vm?.SelectedItem?.Contributors
            .FirstOrDefault(c => c.Role == "Maintainer" && c.Name == vm!.SelectedItem!.MaintainedBy);
        if (contributor is not null)
            ShowContributorFlyout(btn, contributor);
    }

    private static void ShowContributorFlyout(Button anchor, Contributor contributor)
    {
        var flyout = new MenuFlyout();
        var hasLinks = false;

        if (!string.IsNullOrEmpty(contributor.Url))
        {
            var url = contributor.Url;
            var item = new MenuItem
            {
                Header = new TextBlock { Text = "GitHub", Foreground = Brushes.White },
                Icon = new Image
                {
                    Source = new Avalonia.Media.Imaging.Bitmap(
                        AssetLoader.Open(new Uri("avares://XBVault/Assets/Views/ItemDetailWindow/itemdetail-github-20.png"))),
                    Width = 16,
                    Height = 16
                }
            };
            item.Click += (_, _) => OpenUrl(url);
            flyout.Items.Add(item);
            hasLinks = true;
        }

        if (contributor.Donations?.Count > 0)
        {
            foreach (var donation in contributor.Donations)
            {
                var label = donation.Type.ToLowerInvariant() switch
                {
                    "kofi" or "ko-fi" => "Ko-fi",
                    "patreon" => "Patreon",
                    "paypal" => "PayPal",
                    "github_sponsors" => "GitHub Sponsors",
                    "buymeacoffee" => "Buy Me a Coffee",
                    _ => donation.Type
                };
                var donUrl = donation.Url;
                var donItem = new MenuItem
                {
                    Header = new TextBlock { Text = label, Foreground = Brushes.White }
                };
                donItem.Click += (_, _) => OpenUrl(donUrl);
                flyout.Items.Add(donItem);
                hasLinks = true;
            }
        }

        if (!hasLinks)
        {
            flyout.Items.Add(new MenuItem
            {
                Header = new TextBlock { Text = "No links available", Foreground = Brushes.Gray },
                IsEnabled = false
            });
        }

        flyout.ShowAt(anchor);
    }

    private static void OpenUrl(string url)
    {
        try
        {
            PlatformHelper.OpenUrl(url);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Failed to open URL: {url}");
        }
    }
}
