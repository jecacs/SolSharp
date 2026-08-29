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
            error.Details!.Value[1].GetProperty("Custom").GetInt32().Should().Be(6001);
            error.ToString().Should().Contain("Custom(6001)");
        }

        [Test]
        public void ObjectVariant_WithoutInstructionError()
        {
            // Act
            var error = ParseJson("""{"DuplicateInstruction":42}""");

            // Assert
            error!.Kind.Should().Be("DuplicateInstruction");
            error.DuplicateInstructionIndex.Should().Be(42);
            error.InstructionError.Should().BeNull();
            error.Details!.Value.GetInt32().Should().Be(42);
        }

        [TestCase("InsufficientFundsForRent")]
        [TestCase("ProgramExecutionTemporarilyRestricted")]
        public void AccountIndexStructVariant(string kind)
        {
            // Act
            var json = """{"__KIND__":{"account_index":42}}"""
                .Replace("__KIND__", kind, StringComparison.Ordinal);
            var error = ParseJson(json);

            // Assert
            error!.Kind.Should().Be(kind);
            error.AccountIndex.Should().Be(42);
            error.Details!.Value.GetProperty("account_index").GetInt32().Should().Be(42);
            error.ToString().Should().Contain("account 42");
        }

        [Test]
        public void UnknownParameterizedVariant_PreservesPayload()
        {
            // Act
            var error = ParseJson("""{"FutureParameterized":{"value":7}}""");

            // Assert
            error!.Kind.Should().Be("FutureParameterized");
            error.Details!.Value.GetProperty("value").GetInt32().Should().Be(7);
        }

        [TestCase("-1")]
        [TestCase("256")]
        [TestCase("2147483648")]
        public void InstructionIndexOutsideU8_ThrowsJsonException(string index)
        {
            // Arrange
            var json = """{"InstructionError":[__INDEX__,"InvalidArgument"]}"""
                .Replace("__INDEX__", index, StringComparison.Ordinal);

            // Act
            Action act = () => ParseJson(json);

            // Assert
            act.Should().Throw<JsonException>();
        }

        [TestCase("-1")]
        [TestCase("4294967296")]
        public void CustomCodeOutsideU32_ThrowsJsonException(string code)
        {
            // Arrange
            var json = """{"InstructionError":[0,{"Custom":__CODE__}]}"""
                .Replace("__CODE__", code, StringComparison.Ordinal);

            // Act
            Action act = () => ParseJson(json);

            // Assert
            act.Should().Throw<JsonException>();
        }
    }
}
