using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Converters;
using SolSharp.Core.Primitives;
using SolSharp.Rpc.Models;
using SolSharp.Rpc.Protocol;

namespace SolSharp.Rpc.Tests;

public static class RpcJsonTests
{
    [TestFixture]
    public sealed class Options
    {
        [Test]
        public void IsFrozen()
        {
            RpcJson.Options.IsReadOnly.Should().BeTrue();
        }

        [Test]
        public void ResolvesThroughTheSourceGeneratedContextsOnly()
        {
            // Pinning the resolver chain keeps the Native AOT claim honest: a reflection fallback
            // sneaking in here would still pass every functional test while silently breaking AOT
            // publishing. CoreJsonContext must be chained because the Rpc generator cannot materialize
            // Core's converter-attributed primitives (their converters are internal to SolSharp.Core).
            RpcJson.Options.TypeInfoResolverChain.Should().Equal(SolanaJsonContext.Default, CoreJsonContext.Default);
        }

        [Test]
        public void Serialize_UnregisteredType_ThrowsInsteadOfFallingBackToReflection()
        {
            // Act
            Action act = () => JsonSerializer.Serialize(new Unregistered(), RpcJson.Options);

            // Assert
            act.Should().Throw<NotSupportedException>();
        }

        [Test]
        public void DropsNullValuedOptionalsWhenWriting()
        {
            // The request configs rely on WhenWritingNull to keep optional wire fields absent.
            JsonSerializer.Serialize(new CommitmentConfig(), RpcJson.Options).Should().Be("{}");
        }

        [Test]
        public void ReadsPropertyNamesCaseInsensitively()
        {
            // Act
            var value = JsonSerializer.Deserialize<RpcContextValue<ulong>>(
                """{"Context":{"Slot":42},"Value":7}""", RpcJson.Options);

            // Assert
            value!.Context!.Slot.Should().Be(42);
            value.Value.Should().Be(7);
        }

        [Test]
        public void ReadsRpcApiVersionFromResponseContext()
        {
            // Act
            var value = JsonSerializer.Deserialize<RpcContextValue<ulong>>(
                """{"context":{"slot":42,"apiVersion":"3.1.7"},"value":7}""", RpcJson.Options);

            // Assert
            value!.Context!.ApiVersion.Should().Be("3.1.7");
        }

        [Test]
        public void WritesReportedAccountSpaceThroughTheCustomConverter()
        {
            // Arrange
            var account = new AccountInfo
            {
                Owner = new PublicKey(new byte[PublicKey.Length]),
                Space = 3,
                Data = [1, 2, 3]
            };

            // Act
            var json = JsonSerializer.Serialize(account, RpcJson.Options);

            // Assert
            json.Should().Contain("\"space\":3").And.Contain("\"data\":[\"AQID\",\"base64\"]");
        }

        private sealed record Unregistered;
    }

    [TestFixture]
    public sealed class TypeInfo
    {
        [Test]
        public void ReturnsMetadataBoundToTheSharedOptions()
        {
            RpcJson.TypeInfo<RpcRequest>().Options.Should().BeSameAs(RpcJson.Options);
        }

        [Test]
        public void UnregisteredType_Throws()
        {
            // Act
            Action act = () => RpcJson.TypeInfo<Unregistered>();

            // Assert
            act.Should().Throw<NotSupportedException>();
        }

        private sealed record Unregistered;
    }
}
