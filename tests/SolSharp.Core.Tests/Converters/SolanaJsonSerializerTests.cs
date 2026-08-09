using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Converters;
using SolSharp.Core.Primitives;

namespace SolSharp.Core.Tests.Converters;

public static class SolanaJsonSerializerTests
{
    [TestFixture]
    public sealed class Options
    {
        private const string SystemProgram = "11111111111111111111111111111111";

        [Test]
        public void IsFrozen() => SolanaJsonSerializer.Options.IsReadOnly.Should().BeTrue();

        [Test]
        public void SerializesTheCoreWirePrimitives()
        {
            // Act & Assert
            JsonSerializer.Serialize(Commitment.Confirmed, SolanaJsonSerializer.Options)
                .Should().Be("\"confirmed\"");
            JsonSerializer.Serialize(Hash.Parse(SystemProgram), SolanaJsonSerializer.Options)
                .Should().Be($"\"{SystemProgram}\"");
            JsonSerializer.Serialize(PublicKey.Parse(SystemProgram), SolanaJsonSerializer.Options)
                .Should().Be($"\"{SystemProgram}\"");
        }

        [Test]
        public void DeserializesTheCoreWirePrimitives()
        {
            // Act & Assert
            JsonSerializer.Deserialize<Commitment>("\"finalized\"", SolanaJsonSerializer.Options)
                .Should().Be(Commitment.Finalized);
            JsonSerializer.Deserialize<Hash>($"\"{SystemProgram}\"", SolanaJsonSerializer.Options)
                .Should().Be(Hash.Parse(SystemProgram));
            JsonSerializer.Deserialize<PublicKey>($"\"{SystemProgram}\"", SolanaJsonSerializer.Options)
                .Should().Be(PublicKey.Parse(SystemProgram));
        }

        [Test]
        public void SerializesNullablePublicKey()
        {
            // Arrange
            PublicKey? key = PublicKey.Parse(SystemProgram);

            // Act & Assert
            JsonSerializer.Serialize(key, SolanaJsonSerializer.Options)
                .Should().Be($"\"{SystemProgram}\"");
            JsonSerializer.Serialize<PublicKey?>(null, SolanaJsonSerializer.Options)
                .Should().Be("null");
        }

        [Test]
        public void DeserializesNullablePublicKey()
        {
            // Act & Assert
            JsonSerializer.Deserialize<PublicKey?>($"\"{SystemProgram}\"", SolanaJsonSerializer.Options)
                .Should().Be(PublicKey.Parse(SystemProgram));
            JsonSerializer.Deserialize<PublicKey?>("null", SolanaJsonSerializer.Options)
                .Should().BeNull();
        }

        [Test]
        public void Serialize_UnregisteredType_ThrowsInsteadOfFallingBackToReflection()
        {
            // The options are resolved by a source-generated context with no reflection fallback (that
            // fallback is RequiresDynamicCode and would break the Native AOT claim), so an unregistered
            // type must fail loudly rather than quietly resolve through reflection.
            // Act
            Action act = () => JsonSerializer.Serialize(new Unregistered(), SolanaJsonSerializer.Options);

            // Assert
            act.Should().Throw<NotSupportedException>();
        }

        private sealed record Unregistered;
    }
}
