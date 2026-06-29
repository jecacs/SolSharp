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
    // Subjects picked for constant on-chain churn, so a healthy node delivers the first notification within
    // seconds: the SPL Token program sees near-continuous traffic, and the Clock sysvar changes every slot.
    private static readonly PublicKey TokenProgram = PublicKey.Parse(SolanaProgramIds.TokenProgram);
    private static readonly PublicKey Clock = PublicKey.Parse(Sysvars.Clock);

    [TestFixture]
    [Category("Integration")]
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

    // Connects a fresh client, runs the probe under a 30s deadline, and turns transport flakiness (rate
    // limits, timeouts, dropped sockets) into an inconclusive rather than a failure. A real assertion failure
    // is not transient, so it still fails the test.
    private static async Task ProbeAsync(Func<SolanaWsClient, CancellationToken, Task> probe)
    {
        try
        {
            await using var client = new SolanaWsClient();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            await client.ConnectAsync(new Uri(IntegrationEnvironment.WsEndpoint), timeout.Token);
            await probe(client, timeout.Token);
        }
        catch (Exception exception) when (IntegrationEnvironment.IsTransient(exception))
        {
            Assert.Inconclusive($"Skipped: the WebSocket endpoint was unavailable ({exception.GetType().Name}).");
        }
    }
}
