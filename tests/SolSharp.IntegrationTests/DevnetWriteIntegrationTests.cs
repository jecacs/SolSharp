using System.Threading.RateLimiting;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SolSharp.Programs;
using SolSharp.Rpc;
using SolSharp.Wallet;

namespace SolSharp.IntegrationTests;

/// <summary>
/// Live write-path checks: airdrop, transfer, confirmation, and the durable-nonce lifecycle against a real
/// cluster. These are the only tests that submit transactions, so they always target devnet (the public
/// endpoint by default, overridable with <c>SOLSHARP_DEVNET_RPC_URL</c>) - never mainnet. Devnet airdrops
/// are heavily rate-limited; a rejected airdrop or busy node reports inconclusive rather than failing.
/// </summary>
public static class DevnetWriteIntegrationTests
{
    private static readonly TimeSpan ConfirmTimeout = TimeSpan.FromSeconds(60);
    private static readonly TokenBucketRateLimiter RequestLimiter = new(new TokenBucketRateLimiterOptions
    {
        TokenLimit = 1,
        QueueLimit = 128,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        ReplenishmentPeriod = TimeSpan.FromSeconds(1),
        TokensPerPeriod = 1,
        AutoReplenishment = true,
    });

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddSolanaRpc(
            options => options.Endpoint = IntegrationEnvironment.DevnetHttpEndpoint,
            resilience => resilience.RateLimiter.RateLimiter =
                arguments => RequestLimiter.AcquireAsync(1, arguments.Context.CancellationToken));
        return services.BuildServiceProvider();
    }

    /// <summary>Funds a fresh keypair via airdrop and waits for the deposit to confirm.</summary>
    private static async Task<Keypair> FundedPayerAsync(SolanaRpcClient client, ulong lamports)
    {
        var genesisHash = await client.GetGenesisHashAsync();
        IntegrationEnvironment.ValidateDevnetGenesisHash(genesisHash);

        var payer = Keypair.Generate();
        var signature = await client.RequestAirdropAsync(payer.PublicKey, lamports);
        await client.ConfirmTransactionAsync(signature, timeout: ConfirmTimeout);
        return payer;
    }

    [TestFixture]
    [Category("Integration")]
    [Category("DevnetWrite")]
    [NonParallelizable]
    public sealed class SendAndConfirmTransactionAsync
    {
        [Test]
        public async Task AirdropTransferConfirm_RoundTrips()
        {
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();
            using var recipient = Keypair.Generate();

            try
            {
                // Arrange: an airdropped, confirmed payer.
                using var payer = await FundedPayerAsync(client, 1_000_000_000);

                // Act: a real transfer submitted and confirmed on devnet.
                var blockhash = (await client.GetLatestBlockhashAsync()).Blockhash;
                var transaction = new TransactionBuilder()
                    .SetRecentBlockhash(blockhash)
                    .AddInstruction(SystemProgram.Transfer(payer.PublicKey, recipient.PublicKey, 1_000_000))
                    .Build(payer);

                var signature = await client.SendAndConfirmTransactionAsync(transaction.Serialize(), timeout: ConfirmTimeout);

                // Assert
                signature.Should().NotBeNullOrEmpty();
                (await client.GetBalanceAsync(recipient.PublicKey)).Should().Be(1_000_000);
            }
            catch (Exception exception)
            {
                IntegrationEnvironment.RethrowOrInconclusive(exception);
            }
        }
    }

    [TestFixture]
    [Category("Integration")]
    [Category("DevnetWrite")]
    [NonParallelizable]
    public sealed class DurableNonceLifecycle
    {
        [Test]
        public async Task CreateAdvanceAndSpend_EndToEnd()
        {
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();
            using var nonceKeypair = Keypair.Generate();
            using var recipient = Keypair.Generate();

            try
            {
                // Arrange: a funded payer and a created + initialized nonce account.
                using var payer = await FundedPayerAsync(client, 1_000_000_000);

                var rent = await client.GetMinimumBalanceForRentExemptionAsync(SystemProgram.NonceAccountLength);
                var setup = new TransactionBuilder()
                    .SetRecentBlockhash((await client.GetLatestBlockhashAsync()).Blockhash)
                    .AddInstructions(SystemProgram.CreateNonceAccount(
                        payer.PublicKey, nonceKeypair.PublicKey, payer.PublicKey, rent))
                    .Build(payer, nonceKeypair);
                await client.SendAndConfirmTransactionAsync(setup.Serialize(), timeout: ConfirmTimeout);

                var nonce = await client.GetNonceAccountAsync(nonceKeypair.PublicKey);
                nonce.Should().NotBeNull("the nonce account was just created and initialized");
                nonce!.Authority.Should().Be(payer.PublicKey);

                // Act: a transfer anchored to the durable nonce instead of a recent blockhash.
                var transaction = new TransactionBuilder()
                    .SetDurableNonce(nonceKeypair.PublicKey, payer.PublicKey, nonce.Nonce)
                    .AddInstruction(SystemProgram.Transfer(payer.PublicKey, recipient.PublicKey, 1_000))
                    .Build(payer);
                await client.SendAndConfirmTransactionAsync(transaction.Serialize(), timeout: ConfirmTimeout);

                // Assert: the transfer landed and the nonce advanced to a new value.
                (await client.GetBalanceAsync(recipient.PublicKey)).Should().Be(1_000);
                var advanced = await client.GetNonceAccountAsync(nonceKeypair.PublicKey);
                advanced.Should().NotBeNull();
                advanced!.Nonce.Should().NotBe(nonce.Nonce);
            }
            catch (Exception exception)
            {
                IntegrationEnvironment.RethrowOrInconclusive(exception);
            }
        }
    }
}
