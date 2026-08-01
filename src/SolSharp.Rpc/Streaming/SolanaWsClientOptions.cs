namespace SolSharp.Rpc.Streaming;

/// <summary>Tunables for <see cref="SolanaWsClient"/>, most notably the automatic-reconnection policy.</summary>
public sealed record SolanaWsClientOptions
{
    /// <summary>
    /// When <c>true</c> (the default), a dropped connection is re-established and every active
    /// subscription is replayed, so consumers keep reading across the gap. When <c>false</c>, a drop
    /// completes each subscription with an error.
    /// </summary>
    public bool AutoReconnect { get; init; } = true;

    /// <summary>The delay before the first reconnect attempt; it doubles after each failed attempt, up to <see cref="ReconnectMaxDelay"/>.</summary>
    public TimeSpan ReconnectInitialDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>The ceiling for the exponential reconnect backoff.</summary>
    public TimeSpan ReconnectMaxDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>The maximum number of reconnect attempts before giving up; <c>0</c> (the default) retries forever.</summary>
    public int MaxReconnectAttempts { get; init; }

    /// <summary>
    /// The maximum time to wait for a subscribe acknowledgement before failing that subscription. The
    /// default is 30 seconds. This timeout is always finite so one missing acknowledgement cannot block
    /// reconnect replay for every subscription behind it.
    /// </summary>
    public TimeSpan SubscriptionAckTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The maximum encoded size of one incoming WebSocket message, in bytes. The default is 64 MiB.
    /// Messages over the limit close the connection with <c>MessageTooBig</c>.
    /// </summary>
    public int MaxMessageSizeBytes { get; init; } = 64 * 1024 * 1024;

    /// <summary>
    /// The maximum number of unread notifications buffered per subscription. The default is 1,024.
    /// Exceeding the capacity faults and unsubscribes that subscription instead of growing memory without bound.
    /// </summary>
    public int SubscriptionBufferCapacity { get; init; } = 1024;

    /// <summary>
    /// The maximum time to receive the next complete WebSocket message before the connection is treated
    /// as dropped, letting auto-reconnect replace a silently half-open socket. Disabled by default
    /// (<see cref="Timeout.InfiniteTimeSpan"/>): only data messages reset the timer (.NET 8 surfaces no
    /// ping/pong liveness signal), so on a connection whose subscriptions are legitimately quiet - an
    /// account that rarely changes, a pending signature - a timeout would force a reconnect cycle, and a
    /// notification gap, at every interval. Enable it only when the subscribed traffic is guaranteed to be
    /// frequent (slot subscriptions, busy programs) and stuck-connection recovery outweighs idle churn.
    /// </summary>
    public TimeSpan ReceiveTimeout { get; init; } = Timeout.InfiniteTimeSpan;
}
