using System.Diagnostics;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;
using SolSharp.Rpc.Streaming;

namespace SolSharp.IntegrationTests;

/// <summary>
/// Live WebSocket checks against a real Solana cluster (the public mainnet endpoint by default). Each test
/// connects, subscribes, and waits for the first real notification, so it exercises the whole
/// subscribe → notify → unsubscribe path end to end. Tagged <c>Integration</c> and tolerant of an
/// unavailable or rate-limited endpoint (reported inconclusive rather than failed).
/// </summary>
public static class WsIntegrationTests
{
    private static readonly TimeSpan MinimumProbeStartInterval = TimeSpan.FromMilliseconds(500);
    private static readonly SemaphoreSlim ProbeGate = new(1, 1);
    private static readonly Stopwatch ProbeClock = Stopwatch.StartNew();
    private static TimeSpan _nextProbeStart;

    // Subjects picked for constant on-chain churn, so a healthy node delivers the first notification within
    // seconds: the SPL Token program sees near-continuous traffic, and the Clock sysvar changes every slot.
    private static readonly PublicKey TokenProgram = PublicKey.Parse(SolanaProgramIds.TokenProgram);
    private static readonly PublicKey Clock = PublicKey.Parse(Sysvars.Clock);

    [TestFixture]
    [Category("Integration")]
    [NonParallelizable]
    public sealed class SubscribeSlots
    {
        [Test]
        public Task ReceivesNotification() => ProbeAsync(async (client, token) =>
        {
            await foreach (var slot in client.SubscribeSlotsAsync(token).WithCancellation(token))
            {
                slot.Slot.Should().BeGreaterThan(0);
                return;
            }
        });
    }

    [TestFixture]
    [Category("Integration")]
    [NonParallelizable]
    public sealed class SubscribeRoots
    {
        [Test]
        public Task ReceivesRootedSlot() => ProbeAsync(async (client, token) =>
        {
            await foreach (var root in client.SubscribeRootsAsync(token).WithCancellation(token))
            {
                root.Should().BeGreaterThan(0);
                return;
            }
        });
    }

    [TestFixture]
    [Category("Integration")]
    [NonParallelizable]
    public sealed class SubscribeLogs
    {
        [Test]
        public Task ReceivesLogsMentioningTheTokenProgram() => ProbeAsync(async (client, token) =>
        {
            var reader = await client.SubscribeLogsAsync(TokenProgram, cancellationToken: token);
            var note = await reader.ReadAsync(token);

            note.Value!.Signature.Should().NotBeNullOrEmpty();
        });
    }

    [TestFixture]
    [Category("Integration")]
    [NonParallelizable]
    public sealed class SubscribeAccount
    {
        [Test]
        public Task ReceivesAClockUpdate() => ProbeAsync(async (client, token) =>
        {
            var reader = await client.SubscribeAccountAsync(Clock, cancellationToken: token);
            var note = await reader.ReadAsync(token);

            note.Value.Should().NotBeNull();
            note.Value!.Data.Length.Should().BeGreaterThan(0);
        });
    }

    [TestFixture]
    [Category("Integration")]
    [NonParallelizable]
    public sealed class SubscribeParsedAccount
    {
        [Test]
        public Task DecodesAClockUpdate() => ProbeAsync(async (client, token) =>
        {
            var reader = await client.SubscribeParsedAccountAsync(Clock, cancellationToken: token);
            var note = await reader.ReadAsync(token);

            note.Value.Should().NotBeNull();
            // Recognized account → typed Parsed view; unrecognized → raw bytes. Never both null, never dropped.
            (note.Value!.Parsed is not null || note.Value.RawData is not null).Should().BeTrue();
        });
    }

    // Serializes live probes and spaces their starts so independently scheduled fixtures cannot burst a
    // provider's WebSocket request limit. The gate covers the complete subscription lifetime, including
    // unsubscribe, while each probe's 30s deadline starts only after it owns the gate.
    private static async Task ProbeAsync(Func<SolanaWsClient, CancellationToken, Task> probe)
    {
        await ProbeGate.WaitAsync();

        try
        {
            var delay = _nextProbeStart - ProbeClock.Elapsed;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay);

            _nextProbeStart = ProbeClock.Elapsed + MinimumProbeStartInterval;

            try
            {
                await using var client = new SolanaWsClient();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

                await client.ConnectAsync(new Uri(IntegrationEnvironment.WsEndpoint), timeout.Token);
                await probe(client, timeout.Token);
            }
            catch (Exception exception)
            {
                IntegrationEnvironment.RethrowOrInconclusive(exception);
            }
        }
        finally
        {
            ProbeGate.Release();
        }
    }
}
