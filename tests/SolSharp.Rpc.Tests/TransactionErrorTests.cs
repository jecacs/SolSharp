using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Rpc.Models;

namespace SolSharp.Rpc.Tests;

public static class TransactionErrorTests
{
    private static TransactionError? ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return TransactionError.Parse(document.RootElement);
    }

    [TestFixture]
    public sealed class Parse
    {
        [Test]
        public void Null_ReturnsNull()
            => TransactionError.Parse(null).Should().BeNull();

        [Test]
        public void JsonNull_ReturnsNull()
            => ParseJson("null").Should().BeNull();

        [Test]
        public void BareStringVariant()
        {
            // Act
            var error = ParseJson("\"AccountInUse\"");

            // Assert
            error!.Kind.Should().Be("AccountInUse");
            error.InstructionError.Should().BeNull();
        }

        [Test]
        public void InstructionError_NamedVariant()
        {
            // Act
            var error = ParseJson("""{"InstructionError":[1,"InsufficientFunds"]}""");

            // Assert
            error!.Kind.Should().Be("InstructionError");
            error.InstructionIndex.Should().Be(1);
            error.InstructionError!.Kind.Should().Be("InsufficientFunds");
            error.InstructionError.CustomCode.Should().BeNull();
        }

        [Test]
        public void InstructionError_CustomCode()
        {
            // Act
            var error = ParseJson("""{"InstructionError":[2,{"Custom":6001}]}""");

            // Assert
            error!.InstructionIndex.Should().Be(2);
            error.InstructionError!.Kind.Should().Be("Custom");
            error.InstructionError.CustomCode.Should().Be(6001);
            error.ToString().Should().Contain("Custom(6001)");
        }

        [Test]
        public void ObjectVariant_WithoutInstructionError()
        {
            // Act
            var error = ParseJson("""{"DuplicateInstruction":3}""");

            // Assert
            error!.Kind.Should().Be("DuplicateInstruction");
            error.InstructionError.Should().BeNull();
        }
    }
}
