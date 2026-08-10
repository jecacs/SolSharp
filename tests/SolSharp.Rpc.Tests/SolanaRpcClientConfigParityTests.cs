using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Tests;

public static class SolanaRpcClientConfigParityTests
{
    private const string Address = "11111111111111111111111111111111";
    private const string Program = "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA";

    private static (SolanaRpcClient Client, FakeHttpMessageHandler Handler) Make(string resultJson)
    {
        var handler = new FakeHttpMessageHandler(
            $$"""{"jsonrpc":"2.0","result":{{resultJson}},"id":1}""");
        var http = new HttpClient(handler) { BaseAddress = new("http://localhost") };
        return (new(http), handler);
    }

    private static RpcContextOptions ContextOptions() => new()
    {
        Commitment = Commitment.Finalized,
        MinContextSlot = 42
    };

    [TestFixture]
    public sealed class GetLatestBlockhashWithOptionsAsync
    {
        [Test]
        public async Task ContextOptions_SendExactPinnedConfig()
        {
            // Arrange
            var (client, handler) = Make(
                """{"context":{"slot":42},"value":{"blockhash":"abc","lastValidBlockHeight":99}}""");

            // Act
            var value = await client.GetLatestBlockhashWithOptionsAsync(ContextOptions());

            // Assert
            value.LastValidBlockHeight.Should().Be(99);
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getLatestBlockhash","params":[{"commitment":"finalized","minContextSlot":42}]}""");
        }
    }

    [TestFixture]
    public sealed class GetBalanceWithOptionsAsync
    {
        [Test]
        public async Task ContextOptions_SendExactPinnedConfig()
        {
            // Arrange
            var (client, handler) = Make("""{"context":{"slot":42},"value":7}""");

            // Act
            var value = await client.GetBalanceWithOptionsAsync(PublicKey.Parse(Address), ContextOptions());

            // Assert
            value.Should().Be(7);
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getBalance","params":["11111111111111111111111111111111",{"commitment":"finalized","minContextSlot":42}]}""");
        }
    }

    [TestFixture]
    public sealed class GetSlotWithOptionsAsync
    {
        [Test]
        public async Task ContextOptions_SendExactPinnedConfig()
        {
            // Arrange
            var (client, handler) = Make("7");

            // Act
            var value = await client.GetSlotWithOptionsAsync(ContextOptions());

            // Assert
            value.Should().Be(7);
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getSlot","params":[{"commitment":"finalized","minContextSlot":42}]}""");
        }
    }

    [TestFixture]
    public sealed class GetBlockHeightWithOptionsAsync
    {
        [Test]
        public async Task ContextOptions_SendExactPinnedConfig()
        {
            // Arrange
            var (client, handler) = Make("8");

            // Act
            var value = await client.GetBlockHeightWithOptionsAsync(ContextOptions());

            // Assert
            value.Should().Be(8);
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getBlockHeight","params":[{"commitment":"finalized","minContextSlot":42}]}""");
        }
    }

    [TestFixture]
    public sealed class GetTransactionCountWithOptionsAsync
    {
        [Test]
        public async Task ContextOptions_SendExactPinnedConfig()
        {
            // Arrange
            var (client, handler) = Make("9");

            // Act
            var value = await client.GetTransactionCountWithOptionsAsync(ContextOptions());

            // Assert
            value.Should().Be(9);
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getTransactionCount","params":[{"commitment":"finalized","minContextSlot":42}]}""");
        }
    }

    [TestFixture]
    public sealed class GetAccountInfoWithContextAsync
    {
        [Test]
        public async Task FullConfig_ParsesContextAndSendsExactJson()
        {
            // Arrange
            var (client, handler) = Make("""{"context":{"slot":55,"apiVersion":"3.0"},"value":null}""");
            var options = new GetAccountInfoOptions
            {
                Commitment = Commitment.Processed,
                DataSlice = new(3, 5),
                MinContextSlot = 50
            };

            // Act
            var result = await client.GetAccountInfoWithContextAsync(PublicKey.Parse(Address), options);

            // Assert
            result.Context.Slot.Should().Be(55);
            result.Context.ApiVersion.Should().Be("3.0");
            result.Value.Should().BeNull();
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getAccountInfo","params":["11111111111111111111111111111111",{"encoding":"base64","commitment":"processed","dataSlice":{"offset":3,"length":5},"minContextSlot":50}]}""");
        }

        [Test]
        public async Task DataSlicePreservesFullUpstreamUnsignedWidths()
        {
            // Arrange
            var (client, handler) = Make("""{"context":{"slot":55},"value":null}""");
            var options = new GetAccountInfoOptions
            {
                DataSlice = new(2147483648UL, ulong.MaxValue)
            };

            // Act
            await client.GetAccountInfoWithContextAsync(PublicKey.Parse(Address), options);

            // Assert
            handler.CapturedRequestBody.Should().Contain(
                "\"dataSlice\":{\"offset\":2147483648,\"length\":18446744073709551615}");
        }
    }

    [TestFixture]
    public sealed class GetAccountInfoWithOptionsAsync
    {
        [Test]
        public async Task FullConfig_ReturnsValuePath()
        {
            // Arrange
            var (client, handler) = Make("""{"context":{"slot":55},"value":null}""");

            // Act
            var result = await client.GetAccountInfoWithOptionsAsync(
                PublicKey.Parse(Address),
                new() { Encoding = RpcAccountEncoding.Binary, MinContextSlot = 50 });

            // Assert
            result.Should().BeNull();
            handler.CapturedRequestBody.Should().Contain("\"encoding\":\"binary\"");
            handler.CapturedRequestBody.Should().Contain("\"minContextSlot\":50");
        }
    }

    [TestFixture]
    public sealed class GetMultipleAccountsWithContextAsync
    {
        [Test]
        public async Task FullConfig_ParsesContextAndPreservesNullEntries()
        {
            // Arrange
            var (client, handler) = Make("""{"context":{"slot":77},"value":[null]}""");
            var options = new GetAccountInfoOptions
            {
                Commitment = Commitment.Confirmed,
                DataSlice = new(1, 2),
                MinContextSlot = 70
            };

            // Act
            var result = await client.GetMultipleAccountsWithContextAsync(
                [PublicKey.Parse(Address)], options);

            // Assert
            result.Context.Slot.Should().Be(77);
            result.Value.Should().ContainSingle().Which.Should().BeNull();
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getMultipleAccounts","params":[["11111111111111111111111111111111"],{"encoding":"base64","commitment":"confirmed","dataSlice":{"offset":1,"length":2},"minContextSlot":70}]}""");
        }
    }

    [TestFixture]
    public sealed class GetMultipleAccountsWithOptionsAsync
    {
        [Test]
        public async Task FullConfig_ReturnsValuePath()
        {
            // Arrange
            var (client, handler) = Make("""{"context":{"slot":77},"value":[null]}""");

            // Act
            var result = await client.GetMultipleAccountsWithOptionsAsync(
                [PublicKey.Parse(Address)],
                new() { Encoding = RpcAccountEncoding.Base58, MinContextSlot = 70 });

            // Assert
            result.Should().ContainSingle().Which.Should().BeNull();
            handler.CapturedRequestBody.Should().Contain("\"encoding\":\"base58\"");
            handler.CapturedRequestBody.Should().Contain("\"minContextSlot\":70");
        }
    }

    [TestFixture]
    public sealed class GetProgramAccountsWithContextAsync
    {
        [Test]
        public async Task FullConfig_ParsesContextAndSendsExactJson()
        {
            // Arrange
            var (client, handler) = Make("""{"context":{"slot":88},"value":[]}""");
            var options = new GetProgramAccountsOptions
            {
                Commitment = Commitment.Finalized,
                Filters = [AccountFilter.DataSize(165)],
                DataSlice = new(0, 8),
                MinContextSlot = 80,
                SortResults = false
            };

            // Act
            var result = await client.GetProgramAccountsWithContextAsync(PublicKey.Parse(Program), options);

            // Assert
            result.Context.Slot.Should().Be(88);
            result.Value.Should().BeEmpty();
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getProgramAccounts","params":["TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA",{"encoding":"base64","commitment":"finalized","minContextSlot":80,"dataSlice":{"offset":0,"length":8},"filters":[{"dataSize":165}],"withContext":true,"sortResults":false}]}""");
        }
    }

    [TestFixture]
    public sealed class GetProgramAccountsAsync
    {
        [Test]
        public async Task WithContextTrue_ReturnsWrappedValueWithoutLosingCompatibility()
        {
            // Arrange
            var (client, handler) = Make("""{"context":{"slot":88},"value":[]}""");

            // Act
            var result = await client.GetProgramAccountsAsync(
                PublicKey.Parse(Program), new() { WithContext = true });

            // Assert
            result.Should().BeEmpty();
            handler.CapturedRequestBody.Should().Contain("\"withContext\":true");
        }
    }

    [TestFixture]
    public sealed class GetEpochInfoWithOptionsAsync
    {
        [Test]
        public async Task ContextOptions_SendMinContextSlot()
        {
            // Arrange
            var (client, handler) = Make(
                """{"absoluteSlot":42,"blockHeight":40,"epoch":1,"slotIndex":10,"slotsInEpoch":432000}""");

            // Act
            _ = await client.GetEpochInfoWithOptionsAsync(ContextOptions());

            // Assert
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getEpochInfo","params":[{"commitment":"finalized","minContextSlot":42}]}""");
        }
    }

    [TestFixture]
    public sealed class IsBlockhashValidWithOptionsAsync
    {
        [Test]
        public async Task ContextOptions_SendMinContextSlot()
        {
            // Arrange
            var (client, handler) = Make("""{"context":{"slot":42},"value":true}""");

            // Act
            var result = await client.IsBlockhashValidWithOptionsAsync("hash", ContextOptions());

            // Assert
            result.Should().BeTrue();
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"isBlockhashValid","params":["hash",{"commitment":"finalized","minContextSlot":42}]}""");
        }
    }

    [TestFixture]
    public sealed class GetFeeForMessageWithOptionsAsync
    {
        [Test]
        public async Task ContextOptions_SendMinContextSlot()
        {
            // Arrange
            var (client, handler) = Make("""{"context":{"slot":42},"value":5000}""");

            // Act
            var result = await client.GetFeeForMessageWithOptionsAsync([1, 2, 3], ContextOptions());

            // Assert
            result.Should().Be(5000);
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getFeeForMessage","params":["AQID",{"commitment":"finalized","minContextSlot":42}]}""");
        }
    }

    [TestFixture]
    public sealed class RequestAirdropWithOptionsAsync
    {
        [Test]
        public async Task RecentBlockhash_SendsExactPinnedConfig()
        {
            // Arrange
            var (client, handler) = Make("\"signature\"");
            var options = new RequestAirdropOptions
            {
                RecentBlockhash = "recent",
                Commitment = Commitment.Confirmed
            };

            // Act
            var result = await client.RequestAirdropWithOptionsAsync(PublicKey.Parse(Address), 123, options);

            // Assert
            result.Should().Be("signature");
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"requestAirdrop","params":["11111111111111111111111111111111",123,{"recentBlockhash":"recent","commitment":"confirmed"}]}""");
        }
    }

    [TestFixture]
    public sealed class GetTokenAccountsByOwnerWithFilterAsync
    {
        [Test]
        public async Task MintUnionBranch_SendsExactConfig()
        {
            // Arrange
            var (client, handler) = Make("""{"context":{"slot":9},"value":[]}""");

            // Act
            var result = await client.GetTokenAccountsByOwnerWithFilterAsync(
                PublicKey.Parse(Address),
                TokenAccountsFilter.ByMint(PublicKey.Parse(Address)));

            // Assert
            result.Should().BeEmpty();
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getTokenAccountsByOwner","params":["11111111111111111111111111111111",{"mint":"11111111111111111111111111111111"},{"encoding":"base64"}]}""");
        }

        [Test]
        public async Task ProgramIdUnionBranch_SendsExactConfig()
        {
            // Arrange
            var (client, handler) = Make("""{"context":{"slot":9},"value":[]}""");

            // Act
            var result = await client.GetTokenAccountsByOwnerWithFilterAsync(
                PublicKey.Parse(Address),
                TokenAccountsFilter.ByProgramId(PublicKey.Parse(Program)),
                new() { DataSlice = new(0, 0), MinContextSlot = 8 });

            // Assert
            result.Should().BeEmpty();
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getTokenAccountsByOwner","params":["11111111111111111111111111111111",{"programId":"TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA"},{"encoding":"base64","dataSlice":{"offset":0,"length":0},"minContextSlot":8}]}""");
        }
    }

    [TestFixture]
    public sealed class GetTokenAccountsByDelegateWithFilterAsync
    {
        [Test]
        public async Task ProgramIdUnionBranch_SendsExactConfig()
        {
            // Arrange
            var (client, handler) = Make("""{"context":{"slot":9},"value":[]}""");

            // Act
            var result = await client.GetTokenAccountsByDelegateWithFilterAsync(
                PublicKey.Parse(Address),
                TokenAccountsFilter.ByProgramId(PublicKey.Parse(Program)));

            // Assert
            result.Should().BeEmpty();
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getTokenAccountsByDelegate","params":["11111111111111111111111111111111",{"programId":"TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA"},{"encoding":"base64"}]}""");
        }
    }

    [TestFixture]
    public sealed class GetVoteAccountsWithOptionsAsync
    {
        [Test]
        public async Task FullConfig_SendsExactPinnedJson()
        {
            // Arrange
            var (client, handler) = Make("""{"current":[],"delinquent":[]}""");
            var options = new GetVoteAccountsOptions
            {
                VotePublicKey = PublicKey.Parse(Address),
                Commitment = Commitment.Finalized,
                KeepUnstakedDelinquents = true,
                DelinquentSlotDistance = 128
            };

            // Act
            var result = await client.GetVoteAccountsWithOptionsAsync(options);

            // Assert
            result.Current.Should().BeEmpty();
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getVoteAccounts","params":[{"votePubkey":"11111111111111111111111111111111","commitment":"finalized","keepUnstakedDelinquents":true,"delinquentSlotDistance":128}]}""");
        }
    }

    [TestFixture]
    public sealed class GetInflationRewardWithOptionsAsync
    {
        [Test]
        public async Task EpochConfig_SendsMinContextSlot()
        {
            // Arrange
            var (client, handler) = Make("[]");
            var options = new GetInflationRewardOptions
            {
                Epoch = 12,
                Commitment = Commitment.Confirmed,
                MinContextSlot = 99
            };

            // Act
            var result = await client.GetInflationRewardWithOptionsAsync([PublicKey.Parse(Address)], options);

            // Assert
            result.Should().BeEmpty();
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getInflationReward","params":[["11111111111111111111111111111111"],{"commitment":"confirmed","epoch":12,"minContextSlot":99}]}""");
        }
    }

    [TestFixture]
    public sealed class GetLeaderScheduleWithOptionsAsync
    {
        [Test]
        public async Task IdentityFilter_SendsExactPinnedJson()
        {
            // Arrange
            var (client, handler) = Make("{}");
            var options = new GetLeaderScheduleOptions
            {
                Slot = 123,
                Identity = PublicKey.Parse(Address),
                Commitment = Commitment.Finalized
            };

            // Act
            var result = await client.GetLeaderScheduleWithOptionsAsync(options);

            // Assert
            result.Should().BeEmpty();
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getLeaderSchedule","params":[123,{"identity":"11111111111111111111111111111111","commitment":"finalized"}]}""");
        }
    }

    [TestFixture]
    public sealed class GetBlocksWithOptionsAsync
    {
        [Test]
        public async Task ContextOptions_SendMinContextSlot()
        {
            // Arrange
            var (client, handler) = Make("[]");

            // Act
            var result = await client.GetBlocksWithOptionsAsync(10, 20, ContextOptions());

            // Assert
            result.Should().BeEmpty();
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getBlocks","params":[10,20,{"commitment":"finalized","minContextSlot":42}]}""");
        }
    }

    [TestFixture]
    public sealed class GetBlocksWithLimitWithOptionsAsync
    {
        [Test]
        public async Task ContextOptions_SendMinContextSlot()
        {
            // Arrange
            var (client, handler) = Make("[]");

            // Act
            var result = await client.GetBlocksWithLimitWithOptionsAsync(10, 5, ContextOptions());

            // Assert
            result.Should().BeEmpty();
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getBlocksWithLimit","params":[10,5,{"commitment":"finalized","minContextSlot":42}]}""");
        }
    }

    [TestFixture]
    public sealed class GetLargestAccountsWithOptionsAsync
    {
        [Test]
        public async Task SortResults_SendsExactPinnedJson()
        {
            // Arrange
            var (client, handler) = Make("""{"context":{"slot":1},"value":[]}""");

            // Act
            var result = await client.GetLargestAccountsWithOptionsAsync(new()
            {
                Commitment = Commitment.Processed,
                Filter = LargestAccountsFilter.NonCirculating,
                SortResults = false
            });

            // Assert
            result.Should().BeEmpty();
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getLargestAccounts","params":[{"commitment":"processed","filter":"nonCirculating","sortResults":false}]}""");
        }

        [Test]
        public async Task NullEntry_ThrowsJsonException()
        {
            // Arrange
            var (client, _) = Make("""{"context":{"slot":1},"value":[null]}""");

            // Act
            var act = async () => await client.GetLargestAccountsWithOptionsAsync(new());

            // Assert
            await act.Should().ThrowAsync<System.Text.Json.JsonException>();
        }
    }

    [TestFixture]
    public sealed class GetSupplyWithOptionsAsync
    {
        [Test]
        public async Task IncludedList_ParsesPublicKeysAndSendsFalse()
        {
            // Arrange
            var (client, handler) = Make(
                """{"context":{"slot":1},"value":{"total":100,"circulating":90,"nonCirculating":10,"nonCirculatingAccounts":["11111111111111111111111111111111"]}}""");

            // Act
            var result = await client.GetSupplyWithOptionsAsync(new()
            {
                Commitment = Commitment.Finalized,
                ExcludeNonCirculatingAccountsList = false
            });

            // Assert
            result.NonCirculatingAccounts.Should().ContainSingle().Which.Should().Be(PublicKey.Parse(Address));
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getSupply","params":[{"commitment":"finalized","excludeNonCirculatingAccountsList":false}]}""");
        }
    }

    [TestFixture]
    public sealed class GetBlockWithOptionsAsync
    {
        [Test]
        public async Task ExactConfig_ReturnsUnprojectedResponseKat()
        {
            // Arrange
            var (client, handler) = Make(
                """{"blockhash":"b","previousBlockhash":"p","parentSlot":4,"transactions":[{"opaque":true}],"rewards":[{"pubkey":"11111111111111111111111111111111"}]}""");
            var options = new GetBlockOptions
            {
                Encoding = RpcTransactionEncoding.Base64,
                TransactionDetails = RpcTransactionDetails.Full,
                Rewards = true,
                Commitment = Commitment.Finalized,
                MaxSupportedTransactionVersion = 1
            };

            // Act
            var result = await client.GetBlockWithOptionsAsync(5, options);

            // Assert
            result!.Value.GetProperty("transactions")[0].GetProperty("opaque").GetBoolean().Should().BeTrue();
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getBlock","params":[5,{"commitment":"finalized","maxSupportedTransactionVersion":1,"encoding":"base64","transactionDetails":"full","rewards":true}]}""");
        }
    }

    [TestFixture]
    public sealed class GetTransactionWithOptionsAsync
    {
        [Test]
        public async Task ExactEncoding_ReturnsUnprojectedResponseKat()
        {
            // Arrange
            var (client, handler) = Make("""{"slot":5,"transaction":{"message":{"opaque":7}}}""");
            var options = new GetTransactionOptions
            {
                Encoding = RpcTransactionEncoding.Json,
                Commitment = Commitment.Confirmed,
                MaxSupportedTransactionVersion = 1
            };

            // Act
            var result = await client.GetTransactionWithOptionsAsync("signature", options);

            // Assert
            result!.Value.GetProperty("transaction").GetProperty("message").GetProperty("opaque").GetInt32().Should().Be(7);
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getTransaction","params":["signature",{"commitment":"confirmed","maxSupportedTransactionVersion":1,"encoding":"json"}]}""");
        }

        [Test]
        public async Task UnknownEncoding_ThrowsBeforeTransport()
        {
            // Arrange
            var (client, _) = Make("null");
            var options = new GetTransactionOptions { Encoding = (RpcTransactionEncoding)int.MaxValue };

            // Act
            var act = () => client.GetTransactionWithOptionsAsync("signature", options);

            // Assert
            await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        }
    }

    [TestFixture]
    public sealed class GetSlotLeaderWithOptionsAsync
    {
        [Test]
        public async Task ContextOptions_SendMinContextSlot()
        {
            // Arrange
            var (client, handler) = Make("\"11111111111111111111111111111111\"");

            // Act
            var result = await client.GetSlotLeaderWithOptionsAsync(ContextOptions());

            // Assert
            result.Should().Be(PublicKey.Parse(Address));
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getSlotLeader","params":[{"commitment":"finalized","minContextSlot":42}]}""");
        }
    }

    [TestFixture]
    public sealed class GetParsedAccountInfoWithContextAsync
    {
        [Test]
        public async Task ContextOptions_SendExactPinnedConfigAndParseContext()
        {
            // Arrange
            var (client, handler) = Make("""{"context":{"slot":43},"value":null}""");

            // Act
            var result = await client.GetParsedAccountInfoWithContextAsync(
                PublicKey.Parse(Address), ContextOptions());

            // Assert
            result.Context.Slot.Should().Be(43);
            result.Value.Should().BeNull();
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getAccountInfo","params":["11111111111111111111111111111111",{"encoding":"jsonParsed","commitment":"finalized","minContextSlot":42}]}""");
        }
    }

    [TestFixture]
    public sealed class GetStakeMinimumDelegationWithOptionsAsync
    {
        [Test]
        public async Task ContextOptions_SendMinContextSlot()
        {
            // Arrange
            var (client, handler) = Make("""{"context":{"slot":42},"value":1}""");

            // Act
            var result = await client.GetStakeMinimumDelegationWithOptionsAsync(ContextOptions());

            // Assert
            result.Should().Be(1);
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getStakeMinimumDelegation","params":[{"commitment":"finalized","minContextSlot":42}]}""");
        }
    }
}
