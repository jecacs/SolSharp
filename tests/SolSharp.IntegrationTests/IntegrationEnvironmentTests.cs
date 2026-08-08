using FluentAssertions;
using NUnit.Framework;
using SolSharp.Rpc.Protocol;

namespace SolSharp.IntegrationTests;

[TestFixture]
internal sealed class IntegrationEnvironmentTests
{
    [TestCase(-32601)] // Method not found.
    [TestCase(-32602)] // Invalid params.
    [TestCase(-32002)] // Transaction simulation failed.
    [TestCase(-32003)] // Signature verification failure.
    public void IsTransient_DeterministicRpcFailure_ReturnsFalse(int code)
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
    public void IsTransient_TransientRpcFailure_ReturnsTrue(int code)
    {
        // Act
        var result = IntegrationEnvironment.IsTransient(new RpcException(code, "transient"));

        // Assert
        result.Should().BeTrue();
    }
}
