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
}
