using CyberLens.Data;

namespace CyberLens.Services;

/// <summary>
/// In-process pub/sub used to push real-time events (new alerts, fresh posts)
/// from background services into connected Blazor circuits.
/// </summary>
public class NotificationBus
{
    public event Action<Alert>? AlertRaised;
    public event Action<Post>? PostCollected;

    public void PublishAlert(Alert alert) => AlertRaised?.Invoke(alert);
    public void PublishPost(Post post) => PostCollected?.Invoke(post);
}
