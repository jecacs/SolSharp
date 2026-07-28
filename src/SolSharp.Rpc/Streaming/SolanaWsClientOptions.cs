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
    /// The maximum time to receive the next complete WebSocket message. The default is five minutes.
    /// A timeout treats the connection as dropped so auto-reconnect can recover a silent half-open socket.
    /// Set to <see cref="Timeout.InfiniteTimeSpan"/> to disable the timeout.
    /// </summary>
    public TimeSpan ReceiveTimeout { get; init; } = TimeSpan.FromMinutes(5);
}
