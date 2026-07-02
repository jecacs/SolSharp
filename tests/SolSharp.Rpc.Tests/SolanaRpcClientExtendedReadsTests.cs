using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Tests;

public static class SolanaRpcClientExtendedReadsTests
{
    private const string Node = "7QMhYQAPfkoURcrQFxgHKXbipaYL4Sj34kweHx3d3J67";
    private const string Delegate = "9jLkNAaW9E47LQMHvjohy2uAAyr1331bAxgJKFRU7wF6";
    private const string Mint = "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v";

    private static (SolanaRpcClient Client, FakeHttpMessageHandler Handler) Make(string responseJson)
    {
        var handler = new FakeHttpMessageHandler(responseJson);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        return (new SolanaRpcClient(http), handler);
    }

    private static string Result(string valueJson) =>
        """{"jsonrpc":"2.0","result":__VALUE__,"id":1}""".Replace("__VALUE__", valueJson);

    private static string ContextResult(string valueJson) =>
        Result("""{"context":{"slot":1},"value":__VALUE__}""".Replace("__VALUE__", valueJson));

    [TestFixture]
    public sealed class GetBlockCommitmentAsync
    {
        [Test]
        public async Task ParsesCommitmentArrayAndTotalStake()
        {
            // Arrange
            var (client, handler) = Make(Result("""{"commitment":[0,0,500],"totalStake":42000000}"""));

            // Act
            var commitment = await client.GetBlockCommitmentAsync(250000);

            // Assert
            commitment.Commitment.Should().Equal(0ul, 0ul, 500ul);
            commitment.TotalStake.Should().Be(42000000ul);
            handler.CapturedRequestBody.Should().Contain("\"getBlockCommitment\"");
            handler.CapturedRequestBody.Should().Contain("250000");
        }

        [Test]
        public async Task ParsesNullCommitmentForUnknownBlock()
        {
            // Arrange
            var (client, _) = Make(Result("""{"commitment":null,"totalStake":42000000}"""));

            // Act & Assert
            (await client.GetBlockCommitmentAsync(1)).Commitment.Should().BeNull();
        }
    }

    [TestFixture]
    public sealed class GetBlockProductionAsync
    {
        private const string Production =
            """{"byIdentity":{"7QMhYQAPfkoURcrQFxgHKXbipaYL4Sj34kweHx3d3J67":[86,80]},"range":{"firstSlot":100,"lastSlot":200}}""";

        [Test]
        public async Task ParsesByIdentityAndRange()
        {
            // Arrange
            var (client, handler) = Make(ContextResult(Production));

            // Act
            var production = await client.GetBlockProductionAsync();

            // Assert
            production.ByIdentity.Should().ContainKey(Node);
            production.ByIdentity[Node].Should().Equal(86ul, 80ul);
            production.Range.FirstSlot.Should().Be(100ul);
            production.Range.LastSlot.Should().Be(200ul);
            handler.CapturedRequestBody.Should().Contain("\"getBlockProduction\"");
            handler.CapturedRequestBody.Should().NotContain("\"range\"");
        }

        [Test]
        public async Task SendsIdentityAndRange()
        {
            // Arrange
            var (client, handler) = Make(ContextResult(Production));

            // Act
            await client.GetBlockProductionAsync(identity: PublicKey.Parse(Node), firstSlot: 100, lastSlot: 200);

            // Assert
            handler.CapturedRequestBody.Should().Contain($"\"identity\":\"{Node}\"");
            handler.CapturedRequestBody.Should().Contain("\"range\":{\"firstSlot\":100,\"lastSlot\":200}");
        }
    }

    [TestFixture]
    public sealed class GetBlockTimeAsync
    {
        [Test]
        public async Task ParsesUnixTimestamp()
        {
            // Arrange
            var (client, handler) = Make(Result("1700000000"));

            // Act
            var time = await client.GetBlockTimeAsync(250000);

            // Assert
            time.Should().Be(1700000000L);
            handler.CapturedRequestBody.Should().Contain("\"getBlockTime\"");
            handler.CapturedRequestBody.Should().Contain("250000");
        }

        [Test]
        public async Task ReturnsNullWhenUnavailable()
        {
            // Arrange
            var (client, _) = Make(Result("null"));

            // Act & Assert
            (await client.GetBlockTimeAsync(1)).Should().BeNull();
        }
    }

    [TestFixture]
    public sealed class GetBlocksWithLimitAsync
    {
        [Test]
        public async Task ParsesSlotsAndSendsLimit()
        {
            // Arrange
            var (client, handler) = Make(Result("[100,101,102]"));

            // Act
            var blocks = await client.GetBlocksWithLimitAsync(100, 3);

            // Assert
            blocks.Should().Equal(100ul, 101ul, 102ul);
            handler.CapturedRequestBody.Should().Contain("\"getBlocksWithLimit\"");
            handler.CapturedRequestBody.Should().Contain("[100,3,");
        }
    }

    [TestFixture]
    public sealed class GetEpochScheduleAsync
    {
        [Test]
        public async Task ParsesSchedule()
        {
            // Arrange
            var (client, handler) = Make(Result(
                """{"slotsPerEpoch":432000,"leaderScheduleSlotOffset":432000,"warmup":true,"firstNormalEpoch":14,"firstNormalSlot":524256}"""));

            // Act
            var schedule = await client.GetEpochScheduleAsync();

            // Assert
            schedule.SlotsPerEpoch.Should().Be(432000ul);
            schedule.LeaderScheduleSlotOffset.Should().Be(432000ul);
            schedule.Warmup.Should().BeTrue();
            schedule.FirstNormalEpoch.Should().Be(14ul);
            schedule.FirstNormalSlot.Should().Be(524256ul);
            handler.CapturedRequestBody.Should().Contain("\"getEpochSchedule\"");
        }
    }

    [TestFixture]
    public sealed class GetFirstAvailableBlockAsync
    {
        [Test]
        public async Task ParsesSlot()
        {
            // Arrange
            var (client, handler) = Make(Result("250000"));

            // Act & Assert
            (await client.GetFirstAvailableBlockAsync()).Should().Be(250000ul);
            handler.CapturedRequestBody.Should().Contain("\"getFirstAvailableBlock\"");
        }
    }

    [TestFixture]
    public sealed class GetGenesisHashAsync
    {
        [Test]
        public async Task ParsesHash()
        {
            // Arrange
            var (client, handler) = Make(Result("\"5eykt4UsFv8P8NJdTREpY1vzqKqZKvdpKuc147dw2N9d\""));

            // Act & Assert
            (await client.GetGenesisHashAsync()).Should().Be("5eykt4UsFv8P8NJdTREpY1vzqKqZKvdpKuc147dw2N9d");
            handler.CapturedRequestBody.Should().Contain("\"getGenesisHash\"");
        }
    }

    [TestFixture]
    public sealed class GetHighestSnapshotSlotAsync
    {
        [Test]
        public async Task ParsesFullAndIncremental()
        {
            // Arrange
            var (client, handler) = Make(Result("""{"full":250000,"incremental":250100}"""));

            // Act
            var snapshot = await client.GetHighestSnapshotSlotAsync();

            // Assert
            snapshot.Full.Should().Be(250000ul);
            snapshot.Incremental.Should().Be(250100ul);
            handler.CapturedRequestBody.Should().Contain("\"getHighestSnapshotSlot\"");
        }

        [Test]
        public async Task ParsesNullIncremental()
        {
            // Arrange
            var (client, _) = Make(Result("""{"full":250000,"incremental":null}"""));

            // Act & Assert
            (await client.GetHighestSnapshotSlotAsync()).Incremental.Should().BeNull();
        }
    }

    [TestFixture]
    public sealed class GetIdentityAsync
    {
        [Test]
        public async Task UnwrapsIdentityEnvelope()
        {
            // Arrange
            var (client, handler) = Make(Result($$"""{"identity":"{{Node}}"}"""));

            // Act & Assert
            (await client.GetIdentityAsync()).Should().Be(PublicKey.Parse(Node));
            handler.CapturedRequestBody.Should().Contain("\"getIdentity\"");
        }
    }

    [TestFixture]
    public sealed class GetInflationGovernorAsync
    {
        [Test]
        public async Task ParsesGovernor()
        {
            // Arrange
            var (client, handler) = Make(Result(
                """{"initial":0.08,"terminal":0.015,"taper":0.15,"foundation":0.05,"foundationTerm":7.0}"""));

            // Act
            var governor = await client.GetInflationGovernorAsync();

            // Assert
            governor.Initial.Should().Be(0.08);
            governor.Terminal.Should().Be(0.015);
            governor.Taper.Should().Be(0.15);
            governor.Foundation.Should().Be(0.05);
            governor.FoundationTerm.Should().Be(7.0);
            handler.CapturedRequestBody.Should().Contain("\"getInflationGovernor\"");
        }
    }

    [TestFixture]
    public sealed class GetInflationRateAsync
    {
        [Test]
        public async Task ParsesRate()
        {
            // Arrange
            var (client, handler) = Make(Result(
                """{"total":0.062,"validator":0.052,"foundation":0.01,"epoch":600}"""));

            // Act
            var rate = await client.GetInflationRateAsync();

            // Assert
            rate.Total.Should().Be(0.062);
            rate.Validator.Should().Be(0.052);
            rate.Foundation.Should().Be(0.01);
            rate.Epoch.Should().Be(600ul);
            handler.CapturedRequestBody.Should().Contain("\"getInflationRate\"");
        }
    }

    [TestFixture]
    public sealed class GetLargestAccountsAsync
    {
        private const string Accounts =
            """[{"address":"7QMhYQAPfkoURcrQFxgHKXbipaYL4Sj34kweHx3d3J67","lamports":999974},{"address":"9jLkNAaW9E47LQMHvjohy2uAAyr1331bAxgJKFRU7wF6","lamports":42}]""";

        [Test]
        public async Task ParsesAccounts()
        {
            // Arrange
            var (client, handler) = Make(ContextResult(Accounts));

            // Act
            var accounts = await client.GetLargestAccountsAsync();

            // Assert
            accounts.Should().HaveCount(2);
            accounts[0].Address.Should().Be(PublicKey.Parse(Node));
            accounts[0].Lamports.Should().Be(999974ul);
            handler.CapturedRequestBody.Should().Contain("\"getLargestAccounts\"");
            handler.CapturedRequestBody.Should().NotContain("\"filter\"");
        }

        [Test]
        public async Task SendsNonCirculatingFilter()
        {
            // Arrange
            var (client, handler) = Make(ContextResult(Accounts));

            // Act
            await client.GetLargestAccountsAsync(filter: LargestAccountsFilter.NonCirculating);

            // Assert
            handler.CapturedRequestBody.Should().Contain("\"filter\":\"nonCirculating\"");
        }

        [Test]
        public async Task SendsCirculatingFilter()
        {
            // Arrange
            var (client, handler) = Make(ContextResult(Accounts));

            // Act
            await client.GetLargestAccountsAsync(filter: LargestAccountsFilter.Circulating);

            // Assert
            handler.CapturedRequestBody.Should().Contain("\"filter\":\"circulating\"");
        }
    }

    [TestFixture]
    public sealed class GetMaxRetransmitSlotAsync
    {
        [Test]
        public async Task ParsesSlot()
        {
            // Arrange
            var (client, handler) = Make(Result("250000"));

            // Act & Assert
            (await client.GetMaxRetransmitSlotAsync()).Should().Be(250000ul);
            handler.CapturedRequestBody.Should().Contain("\"getMaxRetransmitSlot\"");
        }
    }

    [TestFixture]
    public sealed class GetMaxShredInsertSlotAsync
    {
        [Test]
        public async Task ParsesSlot()
        {
            // Arrange
            var (client, handler) = Make(Result("250000"));

            // Act & Assert
            (await client.GetMaxShredInsertSlotAsync()).Should().Be(250000ul);
            handler.CapturedRequestBody.Should().Contain("\"getMaxShredInsertSlot\"");
        }
    }

    [TestFixture]
    public sealed class GetRecentPerformanceSamplesAsync
    {
        private const string Samples =
            """[{"slot":250000,"numTransactions":126,"numNonVoteTransactions":1,"numSlots":126,"samplePeriodSecs":60}]""";

        [Test]
        public async Task ParsesSamplesAndSendsLimit()
        {
            // Arrange
            var (client, handler) = Make(Result(Samples));

            // Act
            var samples = await client.GetRecentPerformanceSamplesAsync(limit: 1);

            // Assert
            var sample = samples.Should().ContainSingle().Subject;
            sample.Slot.Should().Be(250000ul);
            sample.NumTransactions.Should().Be(126ul);
            sample.NumNonVoteTransactions.Should().Be(1ul);
            sample.NumSlots.Should().Be(126ul);
            sample.SamplePeriodSecs.Should().Be(60);
            handler.CapturedRequestBody.Should().Contain("\"getRecentPerformanceSamples\"");
            handler.CapturedRequestBody.Should().Contain("\"params\":[1]");
        }

        [Test]
        public async Task OmitsLimitWhenNull()
        {
            // Arrange
            var (client, handler) = Make(Result(Samples));

            // Act
            await client.GetRecentPerformanceSamplesAsync();

            // Assert
            handler.CapturedRequestBody.Should().Contain("\"params\":[]");
        }
    }

    [TestFixture]
    public sealed class GetSlotLeaderAsync
    {
        [Test]
        public async Task ParsesLeader()
        {
            // Arrange
            var (client, handler) = Make(Result($"\"{Node}\""));

            // Act & Assert
            (await client.GetSlotLeaderAsync()).Should().Be(PublicKey.Parse(Node));
            handler.CapturedRequestBody.Should().Contain("\"getSlotLeader\"");
        }
    }

    [TestFixture]
    public sealed class GetStakeMinimumDelegationAsync
    {
        [Test]
        public async Task ParsesLamports()
        {
            // Arrange
            var (client, handler) = Make(ContextResult("1000000000"));

            // Act & Assert
            (await client.GetStakeMinimumDelegationAsync()).Should().Be(1000000000ul);
            handler.CapturedRequestBody.Should().Contain("\"getStakeMinimumDelegation\"");
        }
    }

    [TestFixture]
    public sealed class GetTokenAccountsByDelegateAsync
    {
        private const string EntryJson =
            """{"pubkey":"11111111111111111111111111111111","account":{"data":["AQID","base64"],"executable":false,"lamports":2039280,"owner":"TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA","rentEpoch":0,"space":165}}""";

        [Test]
        public async Task ParsesAccountsAndFiltersByMint()
        {
            // Arrange
            var (client, handler) = Make(ContextResult($"[{EntryJson}]"));

            // Act
            var accounts = await client.GetTokenAccountsByDelegateAsync(PublicKey.Parse(Delegate), PublicKey.Parse(Mint));

            // Assert
            accounts.Should().ContainSingle();
            accounts[0].Account.Lamports.Should().Be(2039280);
            handler.CapturedRequestBody.Should().Contain("\"getTokenAccountsByDelegate\"");
            handler.CapturedRequestBody.Should().Contain(Delegate);
            handler.CapturedRequestBody.Should().Contain($"\"mint\":\"{Mint}\"");
            handler.CapturedRequestBody.Should().Contain("\"base64\"");
        }
    }

    [TestFixture]
    public sealed class GetMinimumLedgerSlotAsync
    {
        [Test]
        public async Task ParsesSlotAndSendsWireMethodName()
        {
            // Arrange
            var (client, handler) = Make(Result("250000"));

            // Act & Assert
            (await client.GetMinimumLedgerSlotAsync()).Should().Be(250000ul);
            handler.CapturedRequestBody.Should().Contain("\"minimumLedgerSlot\"");
        }
    }
}
