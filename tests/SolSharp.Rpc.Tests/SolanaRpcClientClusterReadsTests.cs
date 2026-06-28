using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Tests;

public static class SolanaRpcClientClusterReadsTests
{
    private const string Node = "7QMhYQAPfkoURcrQFxgHKXbipaYL4Sj34kweHx3d3J67";
    private const string Vote = "9jLkNAaW9E47LQMHvjohy2uAAyr1331bAxgJKFRU7wF6";

    private static (SolanaRpcClient Client, FakeHttpMessageHandler Handler) Make(string responseJson)
    {
        var handler = new FakeHttpMessageHandler(responseJson);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        return (new SolanaRpcClient(http), handler);
    }

    [TestFixture]
    public sealed class GetVoteAccountsAsync
    {
        [Test]
        public async Task ParsesCurrentAndDelinquent()
        {
            // Arrange
            var (client, _) = Make(Votes);

            // Act
            var votes = await client.GetVoteAccountsAsync();

            // Assert
            votes.Delinquent.Should().BeEmpty();
            var current = votes.Current.Should().ContainSingle().Subject;
            current.VotePubkey.Should().Be(PublicKey.Parse(Vote));
            current.NodePubkey.Should().Be(PublicKey.Parse(Node));
            current.ActivatedStake.Should().Be(42000000ul);
            current.Commission.Should().Be(7);
            current.LastVote.Should().Be(250000ul);
            current.EpochCredits.Should().HaveCount(2);
            current.EpochCredits[1].Should().Equal(601L, 2100L, 1000L);
        }
    }

    [TestFixture]
    public sealed class GetInflationRewardAsync
    {
        [Test]
        public async Task ParsesRewardsAndNullEntries()
        {
            // Arrange
            var (client, handler) = Make(Inflation);

            // Act
            var rewards = await client.GetInflationRewardAsync(
                [PublicKey.Parse(Vote), PublicKey.Parse(Node)], epoch: 600);

            // Assert
            rewards.Should().HaveCount(2);
            rewards[0]!.Epoch.Should().Be(600ul);
            rewards[0]!.Amount.Should().Be(2500ul);
            rewards[0]!.PostBalance.Should().Be(1002500ul);
            rewards[0]!.Commission.Should().BeNull();
            rewards[1].Should().BeNull();
            handler.CapturedRequestBody.Should().Contain("getInflationReward");
            handler.CapturedRequestBody.Should().Contain("600");
        }
    }

    [TestFixture]
    public sealed class GetLeaderScheduleAsync
    {
        [Test]
        public async Task ParsesSchedule()
        {
            // Arrange
            var (client, _) = Make(Schedule);

            // Act
            var schedule = await client.GetLeaderScheduleAsync();

            // Assert
            schedule.Should().NotBeNull();
            schedule!.Should().ContainKey(Node);
            schedule[Node].Should().Equal(0, 1, 2, 3, 4, 5, 6, 7);
        }

        [Test]
        public async Task ReturnsNullWhenNoSchedule()
        {
            // Arrange
            var (client, _) = Make(ScheduleNull);

            // Act & Assert
            (await client.GetLeaderScheduleAsync()).Should().BeNull();
        }
    }

    [TestFixture]
    public sealed class GetBlocksAsync
    {
        [Test]
        public async Task ParsesSlotsAndSendsRange()
        {
            // Arrange
            var (client, handler) = Make(Blocks);

            // Act
            var blocks = await client.GetBlocksAsync(100, 104);

            // Assert
            blocks.Should().Equal(100ul, 101ul, 103ul, 104ul);
            handler.CapturedRequestBody.Should().Contain("getBlocks");
            handler.CapturedRequestBody.Should().Contain("104");
        }
    }

    [TestFixture]
    public sealed class GetClusterNodesAsync
    {
        [Test]
        public async Task ParsesNodes()
        {
            // Arrange
            var (client, _) = Make(Nodes);

            // Act
            var nodes = await client.GetClusterNodesAsync();

            // Assert
            var node = nodes.Should().ContainSingle().Subject;
            node.Pubkey.Should().Be(PublicKey.Parse(Node));
            node.Gossip.Should().Be("10.0.0.1:8001");
            node.Rpc.Should().Be("10.0.0.1:8899");
            node.Version.Should().Be("1.18.5");
            node.FeatureSet.Should().Be(3469865029L);
            node.ShredVersion.Should().Be(50093);
        }
    }

    private const string Votes =
        """{"jsonrpc":"2.0","result":{"current":[{"votePubkey":"9jLkNAaW9E47LQMHvjohy2uAAyr1331bAxgJKFRU7wF6","nodePubkey":"7QMhYQAPfkoURcrQFxgHKXbipaYL4Sj34kweHx3d3J67","activatedStake":42000000,"epochVoteAccount":true,"commission":7,"lastVote":250000,"rootSlot":249968,"epochCredits":[[600,1000,900],[601,2100,1000]]}],"delinquent":[]},"id":1}""";

    private const string Inflation =
        """{"jsonrpc":"2.0","result":[{"epoch":600,"effectiveSlot":259200000,"amount":2500,"postBalance":1002500,"commission":null},null],"id":1}""";

    private const string Schedule =
        """{"jsonrpc":"2.0","result":{"7QMhYQAPfkoURcrQFxgHKXbipaYL4Sj34kweHx3d3J67":[0,1,2,3,4,5,6,7]},"id":1}""";

    private const string ScheduleNull = """{"jsonrpc":"2.0","result":null,"id":1}""";

    private const string Blocks = """{"jsonrpc":"2.0","result":[100,101,103,104],"id":1}""";

    private const string Nodes =
        """{"jsonrpc":"2.0","result":[{"pubkey":"7QMhYQAPfkoURcrQFxgHKXbipaYL4Sj34kweHx3d3J67","gossip":"10.0.0.1:8001","tpu":"10.0.0.1:8003","rpc":"10.0.0.1:8899","version":"1.18.5","featureSet":3469865029,"shredVersion":50093}],"id":1}""";
}
