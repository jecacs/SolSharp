using FluentAssertions;
using NUnit.Framework;
using SolSharp.Rpc.Protocol;

namespace SolSharp.IntegrationTests;

internal static class IntegrationEnvironmentTests
{
    [TestFixture]
    public sealed class IsTransient
    {
        [TestCase(-32601)] // Method not found.
        [TestCase(-32602)] // Invalid params.
        [TestCase(-32002)] // Transaction simulation failed.
        [TestCase(-32003)] // Signature verification failure.
        [TestCase(-32015)] // Unsupported transaction version.
        public void DeterministicRpcFailure_ReturnsFalse(int code)
        {
            // Act
            var result = IntegrationEnvironment.IsTransient(new RpcException(code, "deterministic"));

            // Assert
            result.Should().BeFalse();
        }

        [TestCase(-32603)] // Internal error.
        [TestCase(-32004)] // Block not available for slot.
        [TestCase(-32005)] // Node is unhealthy.
        [TestCase(-32007)] // Slot skipped or missing due to ledger jump.
        [TestCase(-32009)] // Slot missing in long-term storage.
        [TestCase(-32014)] // Block status is not available yet.
        [TestCase(-32016)] // Minimum context slot has not been reached.
        public void TransientRpcFailure_ReturnsTrue(int code)
        {
            // Act
            var result = IntegrationEnvironment.IsTransient(new RpcException(code, "transient"));

            // Assert
            result.Should().BeTrue();
        }

        [TestCase(-32007, true)]
        [TestCase(-32602, false)]
        public void WrappedRpcFailure_UsesInnerRpcClassification(int code, bool expected)
        {
            // Arrange
            var exception = new InvalidOperationException("WebSocket subscription rejected", new RpcException(code, "RPC"));

            // Act
            var result = IntegrationEnvironment.IsTransient(exception);

            // Assert
            result.Should().Be(expected);
        }
    }

    [TestFixture]
    [NonParallelizable]
    public sealed class ReportUnavailableData
    {
        [TestCase(null, false)]
        [TestCase("false", false)]
        [TestCase("1", true)]
        [TestCase("TRUE", true)]
        public void MissingData_RespectsStrictMode(string? strictMode, bool shouldFail)
        {
            // Arrange
            const string variable = "SOLSHARP_INTEGRATION_STRICT";
            const string reason = "No V1 transaction was available in the bounded block sample.";
            var previous = Environment.GetEnvironmentVariable(variable);
            Environment.SetEnvironmentVariable(variable, strictMode);

            try
            {
                // Act
                var act = () => IntegrationEnvironment.ReportUnavailableData(reason);

                // Assert
                if (shouldFail)
                    act.Should().Throw<InvalidOperationException>().WithMessage(reason);
                else
                    act.Should().Throw<InconclusiveException>().WithMessage(reason);
            }
            finally
            {
                Environment.SetEnvironmentVariable(variable, previous);
            }
        }
    }

    [TestFixture]
    public sealed class ValidateDevnetGenesisHash
    {
        [Test]
        public void CanonicalDevnetHash_DoesNotThrow()
        {
            // Act
            var act = static () => IntegrationEnvironment.ValidateDevnetGenesisHash(
                IntegrationEnvironment.DevnetGenesisHash);

            // Assert
            act.Should().NotThrow();
        }

        [TestCase("5eykt4UsFv8P8NJdTREpY1vzqKqZKvdpKuc147dw2N9d")]
        [TestCase("4uhcVJyU9pJkvQyS88uRDiswHXSCkY3zQawwpjk2NsNY")]
        [TestCase("")]
        public void NonDevnetHash_ThrowsBeforeAnyWrite(string genesisHash)
        {
            // Act
            var act = () => IntegrationEnvironment.ValidateDevnetGenesisHash(genesisHash);

            // Assert
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*No write was attempted*");
        }
    }
}
