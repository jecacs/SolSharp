using System.Text.Json;
using System.Threading.RateLimiting;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SolSharp.Core.Primitives;
using SolSharp.Programs;
using SolSharp.Rpc;
using SolSharp.Rpc.Models;
using SolSharp.Rpc.Models.Token2022;

namespace SolSharp.IntegrationTests;

/// <summary>
/// Live read-path checks against a real Solana cluster (the public mainnet endpoint by default). These hit
/// the network, so every fixture is tagged <c>Integration</c> and tolerates rate limits by reporting
/// inconclusive. Exclude them from a fast offline run with <c>dotnet test --filter "TestCategory!=Integration"</c>.
/// </summary>
public static class RpcReadIntegrationTests
{
    // A standard 165-byte SPL token account; only its size matters for the rent-exemption query.
    private const long TokenAccountSize = 165;
    private static readonly TokenBucketRateLimiter RequestLimiter = new(new()
    {
        TokenLimit = 1,
        QueueLimit = 128,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        ReplenishmentPeriod = TimeSpan.FromMilliseconds(500),
        TokensPerPeriod = 1,
        AutoReplenishment = true,
    });

    // USDC: a long-lived, heavily used SPL mint with stable, assertable properties (6 decimals).
    private static readonly PublicKey UsdcMint = PublicKey.Parse("EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v");
    private static readonly PublicKey TokenProgram = PublicKey.Parse("TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA");

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddSolanaRpc(
            static options => options.Endpoint = IntegrationEnvironment.HttpEndpoint,
            static resilience => resilience.RateLimiter.RateLimiter =
                static arguments => RequestLimiter.AcquireAsync(1, arguments.Context.CancellationToken));
        return services.BuildServiceProvider();
    }

    [TestFixture]
    [Category("Integration")]
    public sealed class GetHealthAsync
    {
        [Test]
        public async Task ReportsHealthy()
        {
            // Arrange
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();

            // Act
            var healthy = await IntegrationEnvironment.CallAsync(() => client.GetHealthAsync());

            // Assert
            healthy.Should().BeTrue();
        }
    }

    [TestFixture]
    [Category("Integration")]
    public sealed class GetVersionAsync
    {
        [Test]
        public async Task ReturnsCoreVersion()
        {
            // Arrange
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();

            // Act
            var version = await IntegrationEnvironment.CallAsync(() => client.GetVersionAsync());

            // Assert
            version.SolanaCore.Should().NotBeNullOrEmpty();
        }
    }

    [TestFixture]
    [Category("Integration")]
    public sealed class GetTransactionAsync
    {
        [Test]
        public async Task DefaultReads_DecodeAndVerifyALiveV1Transaction()
        {
            // Arrange
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();
            var signature = await FindV1SignatureAsync(client);

            // Act
            var raw = await IntegrationEnvironment.CallAsync(
                () => client.GetTransactionAsync(signature, Commitment.Finalized));
            var parsed = await IntegrationEnvironment.CallAsync(
                () => client.GetParsedTransactionAsync(signature, Commitment.Finalized));

            // Assert
            raw.Should().NotBeNull("the selected V1 transaction must remain available during the test");
            parsed.Should().NotBeNull();
            raw.Version.Should().Be(RpcTransactionVersion.FromNumber(1));
            parsed.Version.Should().Be(raw.Version);
            parsed.Slot.Should().Be(raw.Slot);
            raw.Transaction[0].Should().Be(MessageV1.VersionPrefix);

            var transaction = Transaction.Deserialize(raw.Transaction);
            transaction.Version.Should().Be(TransactionVersion.V1);
            transaction.Signatures[0].ToString().Should().Be(signature);
            transaction.Serialize().Should().Equal(raw.Transaction);
            transaction.VerifySignatures().Should().BeTrue();

            var message = transaction.Message.Should().BeOfType<MessageV1>().Subject;
            parsed.Signatures.Should().Equal(transaction.Signatures.Select(static value => value.ToString()));
            parsed.Message.AccountKeys.Select(static account => account.Pubkey).Should().Equal(message.AccountKeys);
            parsed.Message.RecentBlockhash.Should().Be(message.LifetimeSpecifier.ToString());
            parsed.Message.AddressTableLookups.Should().BeNull();
            parsed.Message.Instructions.Should().HaveCount(message.Instructions.Count);
            parsed.Message.TransactionConfig.Should().NotBeNull();
            var config = parsed.Message.TransactionConfig!;
            config.PriorityFee.Should().Be(message.Config.PriorityFee);
            config.ComputeUnitLimit.Should().Be(message.Config.ComputeUnitLimit);
            config.LoadedAccountsDataSizeLimit.Should().Be(message.Config.LoadedAccountsDataSizeLimit);
            config.HeapSize.Should().Be(message.Config.HeapSize);
        }

        private static async Task<string> FindV1SignatureAsync(SolanaRpcClient client)
        {
            if (Environment.GetEnvironmentVariable("SOLSHARP_V1_TRANSACTION_SIGNATURE") is { Length: > 0 } configured)
                return configured;

            var slot = await IntegrationEnvironment.CallAsync(() => client.GetSlotAsync(Commitment.Finalized));
            var startSlot = slot > 32 ? slot - 32 : 0;
            var blocks = await IntegrationEnvironment.CallAsync(
                () => client.GetBlocksWithLimitAsync(startSlot, 3, Commitment.Finalized));

            // Bound discovery to three blocks and omit instructions; no cluster-specific address is needed.
            foreach (var blockSlot in blocks.Take(3))
            {
                var block = await IntegrationEnvironment.CallAsync(() => client.GetBlockWithOptionsAsync(blockSlot, new()
                {
                    Commitment = Commitment.Finalized,
                    Encoding = RpcTransactionEncoding.Json,
                    TransactionDetails = RpcTransactionDetails.Accounts,
                    Rewards = false,
                    MaxSupportedTransactionVersion = 1,
                }));

                if (block is not { } value)
                    continue;

                foreach (var transaction in value.GetProperty("transactions").EnumerateArray())
                {
                    var version = transaction.GetProperty("version");
                    if (version.ValueKind == JsonValueKind.Number && version.GetByte() == 1)
                        return transaction.GetProperty("transaction").GetProperty("signatures")[0].GetString()!;
                }
            }

            IntegrationEnvironment.ReportUnavailableData(
                "No V1 transaction was found in at most three recent finalized blocks. "
                + "Set SOLSHARP_V1_TRANSACTION_SIGNATURE to a V1 signature available on the configured cluster.");
            throw new InvalidOperationException("The missing-data handler must end the test.");
        }
    }

    [TestFixture]
    [Category("Integration")]
    public sealed class GetParsedTransactionAsync
    {
        [Test]
        public async Task DecodesARecentMainnetTransaction()
        {
            // Arrange
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();

            // Act
            // A recent signature off a busy mint, then decode it via jsonParsed against the live node.
            var signatures = await IntegrationEnvironment.CallAsync(
                () => client.GetSignaturesForAddressAsync(UsdcMint));
            signatures.Should().NotBeEmpty();

            var parsed = await IntegrationEnvironment.CallAsync(
                () => client.GetParsedTransactionAsync(signatures[0].Signature));

            // Assert
            parsed.Should().NotBeNull();
            parsed.Message.AccountKeys.Should().NotBeEmpty();
            parsed.Message.Instructions.Should().NotBeEmpty();
            // Each instruction is either node-parsed or kept raw - never both null, never dropped.
            parsed.Message.Instructions.Should().OnlyContain(static ix => ix.Parsed != null || ix.Accounts != null);
        }
    }

    [TestFixture]
    [Category("Integration")]
    public sealed class GetParsedAccountInfoAsync
    {
        [Test]
        public async Task DecodesTheUsdcMintAsParsed()
        {
            // Arrange
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();

            // Act
            var account = await IntegrationEnvironment.CallAsync(() => client.GetParsedAccountInfoAsync(UsdcMint));

            // Assert
            account.Should().NotBeNull();
            account.Program.Should().Be("spl-token");
            account.Parsed.Should().NotBeNull();
            account.Parsed!.Type.Should().Be("mint");
        }
    }

    [TestFixture]
    [Category("Integration")]
    public sealed class GetClusterNodesAsync
    {
        [Test]
        public async Task ReturnsNodes()
        {
            // Arrange
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();

            // Act
            var nodes = await IntegrationEnvironment.CallAsync(() => client.GetClusterNodesAsync());

            // Assert
            nodes.Should().NotBeEmpty();
        }
    }

    [TestFixture]
    [Category("Integration")]
    public sealed class GetSlotAsync
    {
        [Test]
        public async Task IsPositive()
        {
            // Arrange
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();

            // Act
            var slot = await IntegrationEnvironment.CallAsync(() => client.GetSlotAsync());

            // Assert
            slot.Should().BeGreaterThan(0);
        }
    }

    [TestFixture]
    [Category("Integration")]
    public sealed class GetEpochInfoAsync
    {
        [Test]
        public async Task HasProgress()
        {
            // Arrange
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();

            // Act
            var epoch = await IntegrationEnvironment.CallAsync(() => client.GetEpochInfoAsync());

            // Assert
            epoch.AbsoluteSlot.Should().BeGreaterThan(0);
            epoch.SlotsInEpoch.Should().BeGreaterThan(0);
        }
    }

    [TestFixture]
    [Category("Integration")]
    public sealed class GetLatestBlockhashAsync
    {
        [Test]
        public async Task IsPopulated()
        {
            // Arrange
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();

            // Act
            var blockhash = await IntegrationEnvironment.CallAsync(() => client.GetLatestBlockhashAsync());

            // Assert
            blockhash.Blockhash.Should().NotBeNullOrEmpty();
            blockhash.LastValidBlockHeight.Should().BeGreaterThan(0);
        }
    }

    [TestFixture]
    [Category("Integration")]
    public sealed class GetSupplyAsync
    {
        [Test]
        public async Task HasCirculatingTotal()
        {
            // Arrange
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();

            // Act
            var supply = await IntegrationEnvironment.CallAsync(() => client.GetSupplyAsync());

            // Assert
            supply.Total.Should().BeGreaterThan(0);
            supply.Circulating.Should().BeGreaterThan(0);
        }
    }

    [TestFixture]
    [Category("Integration")]
    public sealed class GetBalanceAsync
    {
        [Test]
        public async Task OfRentFundedAccount_IsPositive()
        {
            // Arrange
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();

            // Act
            var lamports = await IntegrationEnvironment.CallAsync(() => client.GetBalanceAsync(UsdcMint));

            // Assert
            lamports.Should().BeGreaterThan(0);
        }
    }

    [TestFixture]
    [Category("Integration")]
    public sealed class GetAccountInfoAsync
    {
        [Test]
        public async Task OfMint_IsOwnedByTokenProgram()
        {
            // Arrange
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();

            // Act
            var account = await IntegrationEnvironment.CallAsync(() => client.GetAccountInfoAsync(UsdcMint));

            // Assert
            account.Should().NotBeNull();
            account.Owner.Should().Be(TokenProgram);
            account.Data.Length.Should().Be(Mint.Length);
        }
    }

    [TestFixture]
    [Category("Integration")]
    public sealed class GetMintAsync
    {
        [Test]
        public async Task DecodesUsdcState()
        {
            // Arrange
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();

            // Act
            var mint = await IntegrationEnvironment.CallAsync(() => client.GetMintAsync(UsdcMint));

            // Assert
            mint.Should().NotBeNull();
            mint.Decimals.Should().Be(6);
            mint.IsInitialized.Should().BeTrue();
            mint.Supply.Should().BeGreaterThan(0);
        }
    }

    [TestFixture]
    [Category("Integration")]
    public sealed class GetTokenSupplyAsync
    {
        [Test]
        public async Task OfUsdc_HasSixDecimals()
        {
            // Arrange
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();

            // Act
            var supply = await IntegrationEnvironment.CallAsync(() => client.GetTokenSupplyAsync(UsdcMint));

            // Assert
            supply.Decimals.Should().Be(6);
            supply.Amount.Should().NotBeNullOrEmpty();
            supply.UiAmount.Should().BeGreaterThan(0);
        }
    }

    [TestFixture]
    [Category("Integration")]
    public sealed class GetMinimumBalanceForRentExemptionAsync
    {
        [Test]
        public async Task IsPositive()
        {
            // Arrange
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();

            // Act
            var lamports = await IntegrationEnvironment.CallAsync(
                () => client.GetMinimumBalanceForRentExemptionAsync(TokenAccountSize));

            // Assert
            lamports.Should().BeGreaterThan(0);
        }
    }

    [TestFixture]
    [Category("Integration")]
    public sealed class GetSignaturesForAddressAsync
    {
        [Test]
        public async Task ThenFetchTransaction()
        {
            // Arrange
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();

            // Act
            var signatures = await IntegrationEnvironment.CallAsync(
                () => client.GetSignaturesForAddressAsync(UsdcMint, new() { Limit = 5 }));

            if (signatures.Count == 0)
                Assert.Inconclusive("The endpoint returned no recent signatures for the account.");

            signatures[0].Signature.Should().NotBeNullOrEmpty();

            var transaction = await IntegrationEnvironment.CallAsync(
                () => client.GetTransactionAsync(signatures[0].Signature));

            if (transaction is null)
                Assert.Inconclusive("The referenced transaction was not available from the endpoint.");

            // Assert
            transaction.Slot.Should().BeGreaterThan(0);
        }
    }

    [TestFixture]
    [Category("Integration")]
    public sealed class GetBlockAsync
    {
        [Test]
        public async Task ReturnsRecentBlock()
        {
            // Arrange
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();

            // Act
            var slot = await IntegrationEnvironment.CallAsync(() => client.GetSlotAsync());

            // Step back from the tip: the most recent slots may be skipped or not yet available.
            Block? block = null;
            for (var offset = 32UL; offset <= 160 && block is null; offset += 32)
            {
                var target = slot - offset;
                block = await IntegrationEnvironment.CallAsync(() => client.GetBlockAsync(target));
            }

            if (block is null)
                Assert.Inconclusive("No recent block was available from the endpoint.");

            // Assert
            block.Blockhash.Should().NotBeNullOrEmpty();
            block.ParentSlot.Should().BeGreaterThan(0);
        }
    }

    [TestFixture]
    [Category("Integration")]
    public sealed class Token2022Extensions
    {
        // PYUSD: a long-lived Token-2022 mint that keeps its metadata in the mint itself.
        private static readonly PublicKey PyusdMint = PublicKey.Parse("2b1kV6DkPAnxd5ixfnxCpjxmKwqjjaYmCZfHsFu24GXo");

        [Test]
        public async Task DecodesPyusdMintExtensions()
        {
            // Arrange
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();

            // Act
            var account = await IntegrationEnvironment.CallAsync(() => client.GetAccountInfoAsync(PyusdMint));

            // Assert
            account.Should().NotBeNull();
            var extensions = TokenExtensionSet.DecodeMint(account.Data);
            extensions.Should().NotBeNull("PYUSD is an extended Token-2022 mint");
            extensions.Has(ExtensionType.MetadataPointer).Should().BeTrue();
            extensions.GetMetadataPointer()!.MetadataAddress.Should().Be(PyusdMint, "PYUSD points its metadata at the mint itself");
            // Soft assertion by design: if the in-mint metadata is ever moved, the pointer checks above still hold.
            extensions.GetTokenMetadata()?.Symbol.Should().Be("PYUSD");
        }
    }
}
