using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;
using SolSharp.Rpc.Streaming;

namespace SolSharp.Rpc.Tests;

public static class SolanaRpcClientSourceCompatibilityTests
{
    [TestFixture]
    public sealed class PositionalDefaultLiterals
    {
        [Test]
        public void BindToLegacyOverloadsAtCompileTime()
        {
            // Taking the method containing these calls as a delegate compiles every expression without executing it.
            // Reintroducing a same-name reference-options overload makes this file fail with CS0121.
            Action<SolanaRpcClient, SolanaWsClient, PublicKey> bindingProbe = BindLegacyCalls;

            bindingProbe.Should().NotBeNull();
        }

        private static void BindLegacyCalls(SolanaRpcClient rpc, SolanaWsClient ws, PublicKey account)
        {
            _ = rpc.GetLatestBlockhashAsync(default);
            _ = rpc.GetBalanceAsync(account, default);
            _ = rpc.GetSlotAsync(default);
            _ = rpc.GetBlockHeightAsync(default);
            _ = rpc.GetTransactionCountAsync(default);
            _ = rpc.GetAccountInfoAsync(account, default);
            _ = rpc.GetMultipleAccountsAsync([account], default);
            _ = rpc.GetProgramAccountsAsync(account, default);
            _ = rpc.GetEpochInfoAsync(default);
            _ = rpc.IsBlockhashValidAsync("hash", default);
            _ = rpc.GetFeeForMessageAsync([1], default);
            _ = rpc.RequestAirdropAsync(account, 1, default);
            _ = rpc.GetTokenAccountsByOwnerAsync(account, default);
            _ = rpc.GetTransactionAsync("signature", default);
            _ = rpc.GetSupplyAsync(default);
            _ = rpc.GetBlockAsync(1, default);
            _ = rpc.GetVoteAccountsAsync(default);
            _ = rpc.GetInflationRewardAsync([account], default);
            _ = rpc.GetLeaderScheduleAsync(default);
            _ = rpc.GetBlocksAsync(1, 2, default);
            _ = rpc.GetBlocksWithLimitAsync(1, 2, default);
            _ = rpc.GetLargestAccountsAsync(default);
            _ = rpc.GetSlotLeaderAsync(default);
            _ = rpc.GetStakeMinimumDelegationAsync(default);
            _ = rpc.GetTokenAccountsByDelegateAsync(account, default);
            _ = rpc.GetParsedAccountInfoAsync(account, default);
            _ = ws.SubscribeLogsAsync(default);
            _ = ws.SubscribeSignatureAsync("signature", default);
            _ = ws.SubscribeBlocksAsync(default, default, default);
        }
    }
}
