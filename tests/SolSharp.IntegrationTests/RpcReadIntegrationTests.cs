using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SolSharp.Core.Primitives;
using SolSharp.Rpc;
using SolSharp.Rpc.Models;

namespace SolSharp.IntegrationTests;

/// <summary>
/// Live read-path checks against a real Solana cluster (the public mainnet endpoint by default). These hit
/// the network, so every fixture is tagged <c>Integration</c> and tolerates rate limits by reporting
/// inconclusive. Exclude them from a fast offline run with <c>dotnet test --filter "TestCategory!=Integration"</c>.
/// </summary>
public static class RpcReadIntegrationTests
{
    // USDC: a long-lived, heavily used SPL mint with stable, assertable properties (6 decimals).
    private static readonly PublicKey UsdcMint = PublicKey.Parse("EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v");
    private static readonly PublicKey TokenProgram = PublicKey.Parse("TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA");

    // A standard 165-byte SPL token account; only its size matters for the rent-exemption query.
    private const long TokenAccountSize = 165;

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddSolanaRpc(IntegrationEnvironment.HttpEndpoint);
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
            parsed!.Message.AccountKeys.Should().NotBeEmpty();
            parsed.Message.Instructions.Should().NotBeEmpty();
            // Each instruction is either node-parsed or kept raw - never both null, never dropped.
            parsed.Message.Instructions.Should().OnlyContain(ix => ix.Parsed != null || ix.Accounts != null);
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
            account!.Program.Should().Be("spl-token");
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
            account!.Owner.Should().Be(TokenProgram);
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
            mint!.Decimals.Should().Be(6);
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
                () => client.GetSignaturesForAddressAsync(UsdcMint, new GetSignaturesForAddressOptions { Limit = 5 }));

            if (signatures.Count == 0)
                Assert.Inconclusive("The endpoint returned no recent signatures for the account.");

            signatures[0].Signature.Should().NotBeNullOrEmpty();

            var transaction = await IntegrationEnvironment.CallAsync(
                () => client.GetTransactionAsync(signatures[0].Signature));

            if (transaction is null)
                Assert.Inconclusive("The referenced transaction was not available from the endpoint.");

            // Assert
            transaction!.Slot.Should().BeGreaterThan(0);
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
            block!.Blockhash.Should().NotBeNullOrEmpty();
            block.ParentSlot.Should().BeGreaterThan(0);
        }
    }
}
