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
            var bindingProbe = BindLegacyCalls;

            bindingProbe.Should().NotBeNull();
        }

        private static void BindLegacyCalls(SolanaRpcClient rpc, SolanaWsClient ws, PublicKey account)
        {
            _ = rpc.GetLatestBlockhashAsync();
            _ = rpc.GetBalanceAsync(account);
            _ = rpc.GetSlotAsync();
            _ = rpc.GetBlockHeightAsync();
            _ = rpc.GetTransactionCountAsync();
            _ = rpc.GetAccountInfoAsync(account);
            _ = rpc.GetMultipleAccountsAsync([account]);
            _ = rpc.GetProgramAccountsAsync(account);
            _ = rpc.GetEpochInfoAsync();
            _ = rpc.IsBlockhashValidAsync("hash");
            _ = rpc.GetFeeForMessageAsync([1]);
            _ = rpc.RequestAirdropAsync(account, 1);
            _ = rpc.GetTokenAccountsByOwnerAsync(account, default);
            _ = rpc.GetTransactionAsync("signature");
            _ = rpc.GetSupplyAsync();
            _ = rpc.GetBlockAsync(1);
            _ = rpc.GetVoteAccountsAsync();
            _ = rpc.GetInflationRewardAsync([account]);
            _ = rpc.GetLeaderScheduleAsync();
            _ = rpc.GetBlocksAsync(1, 2);
            _ = rpc.GetBlocksWithLimitAsync(1, 2);
            _ = rpc.GetLargestAccountsAsync();
            _ = rpc.GetSlotLeaderAsync();
            _ = rpc.GetStakeMinimumDelegationAsync();
            _ = rpc.GetTokenAccountsByDelegateAsync(account, default);
            _ = rpc.GetParsedAccountInfoAsync(account);
            _ = ws.SubscribeLogsAsync(default);
            _ = ws.SubscribeSignatureAsync("signature");
            _ = ws.SubscribeBlocksAsync(default, default, default);
        }
    }
}
