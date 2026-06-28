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
            // Arrange
            var account = PublicKey.Parse(SolanaProgramIds.TokenProgram);

            // Act
            var json = Serialize(RpcRequests.GetBalance(account, Commitment.Finalized));

            // Assert
            json.Should().Contain("\"method\":\"getBalance\"");
            json.Should().Contain(SolanaProgramIds.TokenProgram);
            json.Should().Contain("\"finalized\"");
        }
    }

    [TestFixture]
    public sealed class GetLatestBlockhash
    {
        [Test]
        public void BuildsMethodAndCommitment()
        {
            // Act
            var json = Serialize(RpcRequests.GetLatestBlockhash(Commitment.Processed));

            // Assert
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
            // Arrange
            var mint = PublicKey.Parse(SolanaProgramIds.TokenProgram);

            // Act
            var json = Serialize(RpcRequests.GetTokenSupply(mint, Commitment.Confirmed));

            // Assert
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
            // Act
            var json = Serialize(RpcRequests.GetMinimumBalanceForRentExemption(165, Commitment.Confirmed));

            // Assert
            json.Should().Contain("\"method\":\"getMinimumBalanceForRentExemption\"");
            json.Should().Contain("165");
        }
    }
}
