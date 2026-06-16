using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Constants;
using SolSharp.Core.Converters;
using SolSharp.Core.Primitives;
using SolSharp.Rpc.Protocol;

namespace SolSharp.Rpc.Tests;

public static class RpcRequestsTests
{
    private static string Serialize(RpcRequest request)
        => JsonSerializer.Serialize(request, SolanaJsonSerializer.Options);

    [TestFixture]
    public sealed class GetBalance
    {
        [Test]
        public void BuildsMethodAddressAndCommitment()
        {
            var account = PublicKey.Parse(SolanaProgramIds.TokenProgram);

            var json = Serialize(RpcRequests.GetBalance(account, Commitment.Finalized));

            json.Should().Contain("\"method\":\"getBalance\"");
            json.Should().Contain(SolanaProgramIds.TokenProgram); // PublicKey -> base58
            json.Should().Contain("\"finalized\"");               // Commitment -> wire string
        }
    }

    [TestFixture]
    public sealed class GetLatestBlockhash
    {
        [Test]
        public void BuildsMethodAndCommitment()
        {
            var json = Serialize(RpcRequests.GetLatestBlockhash(Commitment.Processed));

            json.Should().Contain("\"method\":\"getLatestBlockhash\"");
            json.Should().Contain("\"processed\"");
        }
    }

    [TestFixture]
    public sealed class ParameterlessMethods
    {
        [Test]
        public void GetHealth_HasEmptyParams()
        {
            Serialize(RpcRequests.GetHealth()).Should().Contain("\"params\":[]");
        }

        [Test]
        public void GetVersion_SetsMethod()
        {
            Serialize(RpcRequests.GetVersion()).Should().Contain("\"method\":\"getVersion\"");
        }
    }

    [TestFixture]
    public sealed class GetTokenSupply
    {
        [Test]
        public void BuildsMethodAndMint()
        {
            var mint = PublicKey.Parse(SolanaProgramIds.TokenProgram);

            var json = Serialize(RpcRequests.GetTokenSupply(mint, Commitment.Confirmed));

            json.Should().Contain("\"method\":\"getTokenSupply\"");
            json.Should().Contain(SolanaProgramIds.TokenProgram);
        }
    }

    [TestFixture]
    public sealed class GetMinimumBalanceForRentExemption
    {
        [Test]
        public void BuildsMethodAndDataLength()
        {
            var json = Serialize(RpcRequests.GetMinimumBalanceForRentExemption(165, Commitment.Confirmed));

            json.Should().Contain("\"method\":\"getMinimumBalanceForRentExemption\"");
            json.Should().Contain("165");
        }
    }
}
