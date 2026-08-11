using System;
using System.Collections.Generic;

namespace XBVault.Models;

public sealed class NotificationAction
{
    public string Label { get; init; } = string.Empty;
    public Action? Action { get; init; }
}

public sealed class NotificationItem
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? IconUri { get; init; }
    public Action? ClickAction { get; init; }
    public IReadOnlyList<NotificationAction> Actions { get; init; } = [];
    public bool IsGrouped => Actions.Count > 0;
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
}
