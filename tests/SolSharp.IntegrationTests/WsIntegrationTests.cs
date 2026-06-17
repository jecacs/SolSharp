using FluentAssertions;
using NUnit.Framework;
using SolSharp.Rpc.Streaming;

namespace SolSharp.IntegrationTests;

/// <summary>
/// Live WebSocket checks against a real Solana cluster (the public mainnet endpoint by default). Tagged
/// <c>Integration</c> and tolerant of an unavailable or rate-limited endpoint (reported inconclusive
/// rather than failed).
/// </summary>
public static class WsIntegrationTests
{
    [TestFixture]
    [Category("Integration")]
    public sealed class SubscribeSlots
    {
        [Test]
        public async Task ReceivesNotification()
        {
            SlotInfo? observed = null;
            try
            {
                await using var client = new SolanaWsClient();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

                await client.ConnectAsync(new Uri(IntegrationEnvironment.WsEndpoint), timeout.Token);

                await foreach (var slot in client.SubscribeSlotsAsync(timeout.Token).WithCancellation(timeout.Token))
                {
                    observed = slot;
                    break;
                }
            }
            catch (Exception exception) when (IntegrationEnvironment.IsTransient(exception))
            {
                Assert.Inconclusive($"Skipped: the WebSocket endpoint was unavailable ({exception.GetType().Name}).");
            }

            observed.Should().NotBeNull();
            observed!.Slot.Should().BeGreaterThan(0);
        }
    }
}
