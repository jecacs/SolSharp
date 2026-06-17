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
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();

            var healthy = await IntegrationEnvironment.CallAsync(() => client.GetHealthAsync());

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
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();

            var version = await IntegrationEnvironment.CallAsync(() => client.GetVersionAsync());

            version.SolanaCore.Should().NotBeNullOrEmpty();
        }
    }

    [TestFixture]
    [Category("Integration")]
    public sealed class GetSlotAsync
    {
        [Test]
        public async Task IsPositive()
        {
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();

            var slot = await IntegrationEnvironment.CallAsync(() => client.GetSlotAsync());

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
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();

            var epoch = await IntegrationEnvironment.CallAsync(() => client.GetEpochInfoAsync());

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
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();

            var blockhash = await IntegrationEnvironment.CallAsync(() => client.GetLatestBlockhashAsync());

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
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();

            var supply = await IntegrationEnvironment.CallAsync(() => client.GetSupplyAsync());

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
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();

            // The USDC mint account itself holds rent-exempt lamports.
            var lamports = await IntegrationEnvironment.CallAsync(() => client.GetBalanceAsync(UsdcMint));

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
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();

            var account = await IntegrationEnvironment.CallAsync(() => client.GetAccountInfoAsync(UsdcMint));

            account.Should().NotBeNull();
            account!.Owner.Should().Be(TokenProgram);
            account.Data.Length.Should().Be(Mint.Length); // 82-byte SPL mint layout
        }
    }

    [TestFixture]
    [Category("Integration")]
    public sealed class GetMintAsync
    {
        [Test]
        public async Task DecodesUsdcState()
        {
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();

            var mint = await IntegrationEnvironment.CallAsync(() => client.GetMintAsync(UsdcMint));

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
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();

            var supply = await IntegrationEnvironment.CallAsync(() => client.GetTokenSupplyAsync(UsdcMint));

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
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();

            var lamports = await IntegrationEnvironment.CallAsync(
                () => client.GetMinimumBalanceForRentExemptionAsync(TokenAccountSize));

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
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();

            var signatures = await IntegrationEnvironment.CallAsync(
                () => client.GetSignaturesForAddressAsync(UsdcMint, new GetSignaturesForAddressOptions { Limit = 5 }));

            if (signatures.Count == 0)
                Assert.Inconclusive("The endpoint returned no recent signatures for the account.");

            signatures[0].Signature.Should().NotBeNullOrEmpty();

            var transaction = await IntegrationEnvironment.CallAsync(
                () => client.GetTransactionAsync(signatures[0].Signature));

            if (transaction is null)
                Assert.Inconclusive("The referenced transaction was not available from the endpoint.");

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
            using var provider = CreateProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();

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

            block!.Blockhash.Should().NotBeNullOrEmpty();
            block.ParentSlot.Should().BeGreaterThan(0);
        }
    }
}
