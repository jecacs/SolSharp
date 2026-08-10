using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Converters;
using SolSharp.Core.Primitives;
using SolSharp.Rpc.Models;
using SolSharp.Rpc.Protocol;
using SolSharp.Rpc.Streaming;

namespace SolSharp.Rpc.Tests;

public static class RpcJsonTests
{
    public static class SignatureNotificationJsonConverter
    {
        [TestFixture]
        public sealed class Read
        {
            [Test]
            public void Null_ThrowsJsonException()
            {
                // Act
                Action act = () => JsonSerializer.Deserialize<SignatureNotification>("null", RpcJson.Options);

                // Assert
                act.Should().Throw<JsonException>();
            }
        }

        [TestFixture]
        public sealed class Write
        {
            [Test]
            public void Processed_SerializesPriorCompatibleErrorObject()
            {
                // Arrange
                using var errorDocument = JsonDocument.Parse("""{"InstructionError":[0,"Custom"]}""");
                var notification = new SignatureNotification
                {
                    Kind = SignatureNotificationKind.Processed,
                    Err = errorDocument.RootElement.Clone()
                };

                // Act
                var json = JsonSerializer.Serialize(notification, RpcJson.Options);

                // Assert
                json.Should().Be("""{"err":{"InstructionError":[0,"Custom"]}}""");
            }

            [Test]
            public void ProcessedSuccess_SerializesMandatoryNullError()
            {
                // Act
                var json = JsonSerializer.Serialize(new SignatureNotification(), RpcJson.Options);

                // Assert
                json.Should().Be("""{"err":null}""");
            }

            [Test]
            public void Received_SerializesExactUnionString()
            {
                // Arrange
                var notification = new SignatureNotification { Kind = SignatureNotificationKind.Received };

                // Act
                var json = JsonSerializer.Serialize(notification, RpcJson.Options);

                // Assert
                json.Should().Be("\"receivedSignature\"");
            }

            [Test]
            public void ReceivedWithError_ThrowsJsonException()
            {
                // Arrange
                using var errorDocument = JsonDocument.Parse("1");
                var notification = new SignatureNotification
                {
                    Kind = SignatureNotificationKind.Received,
                    Err = errorDocument.RootElement.Clone()
                };

                // Act
                Action act = () => JsonSerializer.Serialize(notification, RpcJson.Options);

                // Assert
                act.Should().Throw<JsonException>();
            }
        }
    }

    [TestFixture]
    public sealed class Options
    {
        [Test]
        public void IsFrozen() => RpcJson.Options.IsReadOnly.Should().BeTrue();

        [Test]
        public void ResolvesThroughTheSourceGeneratedContextsOnly() =>
            // Pinning the resolver chain keeps the Native AOT claim honest: a reflection fallback
            // sneaking in here would still pass every functional test while silently breaking AOT
            // publishing. CoreJsonContext must be chained because the Rpc generator cannot materialize
            // Core's converter-attributed primitives from another source-generated assembly.
            RpcJson.Options.TypeInfoResolverChain.Should().Equal(SolanaJsonContext.Default, CoreJsonContext.Default);

        [Test]
        public void Serialize_UnregisteredType_ThrowsInsteadOfFallingBackToReflection()
        {
            // Act
            Action act = () => JsonSerializer.Serialize(new Unregistered(), RpcJson.Options);

            // Assert
            act.Should().Throw<NotSupportedException>();
        }

        [Test]
        public void DropsNullValuedOptionalsWhenWriting() =>
            // The request configs rely on WhenWritingNull to keep optional wire fields absent.
            JsonSerializer.Serialize(new CommitmentConfig(), RpcJson.Options).Should().Be("{}");

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
        public void ReturnsMetadataBoundToTheSharedOptions() => RpcJson.TypeInfo<RpcRequest>().Options.Should().BeSameAs(RpcJson.Options);

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

    [TestFixture]
    public sealed class RpcContextValue
    {
        [TestCase("{\"value\":7}")]
        [TestCase("{\"context\":null,\"value\":7}")]
        [TestCase("{\"context\":{},\"value\":7}")]
        [TestCase("{\"context\":{\"slot\":1}}")]
        public void MissingMandatoryWrapperMember_ThrowsJsonException(string json)
        {
            // Act
            Action act = () => JsonSerializer.Deserialize<SolSharp.Rpc.Protocol.RpcContextValue<ulong?>>(
                json, RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>();
        }

        [Test]
        public void ExplicitNullableValue_IsPreserved()
        {
            // Act
            var value = JsonSerializer.Deserialize<SolSharp.Rpc.Protocol.RpcContextValue<ulong?>>(
                """{"context":{"slot":1},"value":null}""", RpcJson.Options);

            // Assert
            value!.Context!.Slot.Should().Be(1);
            value.Value.Should().BeNull();
        }

        [Test]
        public void ProgrammaticMissingContext_ThrowsInvalidOperationException()
        {
            // Arrange
            var value = new SolSharp.Rpc.Protocol.RpcContextValue<ulong>();

            // Act
            Action act = () => _ = value.Context;

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }
    }

    [TestFixture]
    public sealed class MandatoryStreamingModels
    {
        [Test]
        public void MissingLogsMembers_ThrowsJsonException()
        {
            // Act
            Action act = () => JsonSerializer.Deserialize<LogInfo>("{}", RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>();
        }

        [Test]
        public void MissingSlotMembers_ThrowsJsonException()
        {
            // Act
            Action act = () => JsonSerializer.Deserialize<SlotInfo>("{}", RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>();
        }

        [Test]
        public void MissingVoteMembers_ThrowsJsonException()
        {
            // Act
            Action act = () => JsonSerializer.Deserialize<VoteNotification>("{}", RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>();
        }

        [TestCase("{}")]
        [TestCase("{\"slot\":1,\"type\":\"futureStage\",\"timestamp\":1}")]
        [TestCase("{\"slot\":1,\"type\":\"createdBank\",\"timestamp\":1}")]
        [TestCase("{\"slot\":1,\"type\":\"frozen\",\"timestamp\":1}")]
        [TestCase("{\"slot\":1,\"type\":\"dead\",\"timestamp\":1}")]
        public void MalformedSlotUpdate_ThrowsJsonException(string json)
        {
            // Act
            Action act = () => JsonSerializer.Deserialize<SlotsUpdate>(json, RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>();
        }

        [Test]
        public void MissingRawBlockMembers_ThrowsJsonException()
        {
            // Act
            Action act = () => JsonSerializer.Deserialize<RawBlockNotification>("{}", RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>();
        }

        [Test]
        public void RawBlockWithNeitherBodyNorError_ThrowsJsonException()
        {
            // Act
            Action act = () => JsonSerializer.Deserialize<RawBlockNotification>(
                """{"slot":1,"block":null,"err":null}""", RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>();
        }

        [Test]
        public void MissingBlockMembers_ThrowsJsonException()
        {
            // Act
            Action act = () => JsonSerializer.Deserialize<BlockNotification>("{}", RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>();
        }

        [Test]
        public void MissingParsedBlockMembers_ThrowsJsonException()
        {
            // Act
            Action act = () => JsonSerializer.Deserialize<ParsedBlockNotification>("{}", RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>();
        }
    }
}
