using System;
using System.Collections.Generic;
using System.Linq;

namespace XBVault.Models;

public sealed class NotificationAction
{
    public string Label { get; init; } = string.Empty;
    public Action? Action { get; init; }
}

public sealed class NotificationItem
{
    public const int ToastVisibleActionLimit = 5;

    public Guid Id { get; } = Guid.NewGuid();
    public string Tag { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? IconUri { get; init; }
    public Action? ClickAction { get; init; }
    public IReadOnlyList<NotificationAction> Actions { get; init; } = [];
    public bool IsGrouped => Actions.Count > 0;
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    public IReadOnlyList<NotificationAction> ToastVisibleActions => Actions.Take(ToastVisibleActionLimit).ToList();
    public int ToastMoreCount => Math.Max(0, Actions.Count - ToastVisibleActionLimit);
    public bool HasMore => Actions.Count > ToastVisibleActionLimit;
}
