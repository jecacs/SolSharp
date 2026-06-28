using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Core.Tests.Primitives;

public static class CommitmentTests
{
    // Serialize with default options on purpose: the [JsonConverter] attribute must hold
    // the wire mapping even when no SolanaJsonSerializer.Options are passed.
    [TestFixture]
    public sealed class Serialize
    {
        [TestCase(Commitment.Confirmed, "confirmed")]
        [TestCase(Commitment.Finalized, "finalized")]
        [TestCase(Commitment.Processed, "processed")]
        public void ProducesWireString(Commitment value, string expected)
            => JsonSerializer.Serialize(value).Should().Be($"\"{expected}\"");
    }

    [TestFixture]
    public sealed class Deserialize
    {
        [TestCase("confirmed", Commitment.Confirmed)]
        [TestCase("finalized", Commitment.Finalized)]
        [TestCase("processed", Commitment.Processed)]
        public void ParsesWireString(string wire, Commitment expected)
            => JsonSerializer.Deserialize<Commitment>($"\"{wire}\"").Should().Be(expected);

        [Test]
        public void UnknownValue_Throws()
        {
            // Act
            Action act = () => JsonSerializer.Deserialize<Commitment>("\"bogus\"");

            // Assert
            act.Should().Throw<JsonException>();
        }
    }
}
