namespace TrustFlow.Api.Dtos.Notifications;

public sealed class NotificationItemResponse
{
    // Stable across polls so the client can key a list without flicker.
    public string Id { get; init; } = string.Empty;

    public string Kind { get; init; } = string.Empty;

    // "action" needs the viewer to do something, "warning" says something is
    // blocked, "info" is progress the viewer is waiting on.
    public string Severity { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string Href { get; init; } = string.Empty;

    public string ProjectTitle { get; init; } = string.Empty;

    public bool CanMarkRead { get; init; }
}

public sealed class NotificationsResponse
{
    public IReadOnlyList<NotificationItemResponse> Items { get; init; } = [];

    // Actions/warnings plus unread durable notices waiting to be seen.
    public int ActionCount { get; init; }
}
