using XBVault.Models;
using XBVault.Services;

namespace XBVault.Tests;

public class NotificationCenterServiceTests
{
    private sealed class InlineNotifications : NotificationCenterService
    {
        protected override void PostToUi(Action action) => action();
    }

    private static NotificationAction Action(string label = "app") => new() { Label = label, Action = () => { } };

    [Fact]
    public void NotifyGrouped_SetsTitleActionsAndCount()
    {
        var center = new InlineNotifications();

        var item = center.NotifyGrouped("2 app updates available", [Action("a"), Action("b")]);

        Assert.Equal("2 app updates available", item.Title);
        Assert.Equal("2 notifications", item.Message);
        Assert.Equal(2, item.Actions.Count);
        Assert.Single(center.Active);
        Assert.Equal(1, center.UnacknowledgedCount);
        Assert.Empty(center.History);
    }

    [Fact]
    public void NotifyGrouped_SingleItem_MessageIsSingular()
    {
        var center = new InlineNotifications();

        var item = center.NotifyGrouped("1 app update available", [Action("a")]);

        Assert.Equal("1 notification", item.Message);
    }

    [Fact]
    public async Task AutoDismiss_TrueMovesToHistory_PersistentStaysActive()
    {
        var center = new InlineNotifications();
        var persistent = center.NotifyGrouped("updates", [Action("a")], autoDismiss: false);
        var ephemeral = center.NotifyGrouped("transient", [Action("b")], autoDismiss: true);

        await Task.Delay(NotificationCenterService.DefaultAutoDismiss + TimeSpan.FromSeconds(1));

        Assert.Contains(persistent, center.Active);
        Assert.DoesNotContain(ephemeral, center.Active);
        Assert.Contains(ephemeral, center.History);
        Assert.Equal(1, center.UnacknowledgedCount);
    }

    [Fact]
    public void Dismiss_Persistent_MovesToHistoryAndDecrements()
    {
        var center = new InlineNotifications();
        var item = center.NotifyGrouped("updates", [Action("a")], autoDismiss: false);

        center.Dismiss(item.Id);

        Assert.Empty(center.Active);
        Assert.Single(center.History);
        Assert.Equal(0, center.UnacknowledgedCount);
    }
}
