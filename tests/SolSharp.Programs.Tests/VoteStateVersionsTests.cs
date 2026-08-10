using System.Buffers.Binary;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs.Tests;

public static class VoteStateVersionsTests
{
    private static byte[] BuildLegacyVector(VoteStateVersion version)
    {
        using var stream = new MemoryStream();
        WriteUInt32(stream, (uint)version);
        WriteRepeated(stream, 1, 32);
        WriteRepeated(stream, 2, 32);
        stream.WriteByte(3);
        WriteUInt64(stream, 1);
        if (version is VoteStateVersion.V3)
            stream.WriteByte(17);
        WriteUInt64(stream, 4);
        WriteUInt32(stream, 5);
        stream.WriteByte(1);
        WriteUInt64(stream, 6);
        WriteUInt64(stream, 1);
        WriteUInt64(stream, 7);
        WriteRepeated(stream, 8, 32);
        WriteRepeated(stream, 9, 32);
        WriteUInt64(stream, 10);
        WriteUInt64(stream, 11);
        for (var i = 1; i < VoteStateVersions.PriorVoterEntries; i++)
            WriteRepeated(stream, 0, 48);
        WriteUInt64(stream, 31);
        stream.WriteByte(0);
        WriteUInt64(stream, 1);
        WriteUInt64(stream, 12);
        WriteUInt64(stream, 13);
        WriteUInt64(stream, 14);
        WriteUInt64(stream, 15);
        WriteInt64(stream, -16);
        return stream.ToArray();
    }

    private static byte[] BuildV4Vector()
    {
        using var stream = new MemoryStream();
        WriteUInt32(stream, (uint)VoteStateVersion.V4);
        WriteRepeated(stream, 21, 32);
        WriteRepeated(stream, 22, 32);
        WriteRepeated(stream, 23, 32);
        WriteRepeated(stream, 24, 32);
        WriteUInt16(stream, 2_526);
        WriteUInt16(stream, 2_728);
        WriteUInt64(stream, 29);
        stream.WriteByte(1);
        WriteRepeated(stream, 30, VoteStateVersions.BlsPublicKeyLength);
        WriteUInt64(stream, 1);
        stream.WriteByte(31);
        WriteUInt64(stream, 32);
        WriteUInt32(stream, 33);
        stream.WriteByte(1);
        WriteUInt64(stream, 34);
        WriteUInt64(stream, 1);
        WriteUInt64(stream, 35);
        WriteRepeated(stream, 36, 32);
        WriteUInt64(stream, 1);
        WriteUInt64(stream, 37);
        WriteUInt64(stream, 38);
        WriteUInt64(stream, 39);
        WriteUInt64(stream, 40);
        WriteInt64(stream, -41);
        return stream.ToArray();
    }

    private static void WriteRepeated(MemoryStream stream, byte value, int length)
    {
        for (var i = 0; i < length; i++)
            stream.WriteByte(value);
    }

    private static void WriteUInt16(MemoryStream stream, ushort value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteUInt32(MemoryStream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteUInt64(MemoryStream stream, ulong value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteInt64(MemoryStream stream, long value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    [TestFixture]
    public sealed class Parse
    {
        [Test]
        public void PinnedV1BincodeVector_PreservesLegacyVariantAndFieldOrder()
        {
            // Arrange
            var data = BuildLegacyVector(VoteStateVersion.V1_14_11);

            // Act
            var state = VoteStateVersions.Parse(data).Should().BeOfType<VoteStateV1_14_11>().Subject;

            // Assert
            state.Version.Should().Be(VoteStateVersion.V1_14_11);
            state.Node.ToBytes().Should().OnlyContain(value => value == 1);
            state.AuthorizedWithdrawer.ToBytes().Should().OnlyContain(value => value == 2);
            state.Commission.Should().Be(3);
            state.Votes.Should().Equal(new VoteStateLockout(0, 4, 5));
            state.RootSlot.Should().Be(6);
            state.AuthorizedVoters.Should().Equal(
                new AuthorizedVoteVoter(7, new PublicKey(Enumerable.Repeat((byte)8, 32).ToArray())));
            state.PriorVoters.Should().HaveCount(VoteStateVersions.PriorVoterEntries);
            state.PriorVoters[0].Should().Be(
                new PriorVoteVoter(new PublicKey(Enumerable.Repeat((byte)9, 32).ToArray()), 10, 11));
            state.PriorVoterIndex.Should().Be(31);
            state.PriorVotersEmpty.Should().BeFalse();
            state.EpochCredits.Should().Equal(new VoteEpochCredits(12, 13, 14));
            state.LastTimestamp.Should().Be(new VoteStateTimestamp(15, -16));
            state.IsUninitialized.Should().BeFalse();
            VoteStateV1_14_11.DataLength.Should().Be(3_731);
        }

        [Test]
        public void PinnedV3BincodeVector_ReadsLatencyBeforeLockout()
        {
            // Arrange
            var data = BuildLegacyVector(VoteStateVersion.V3);

            // Act
            var state = VoteStateVersions.Parse(data).Should().BeOfType<VoteStateV3>().Subject;

            // Assert
            state.Version.Should().Be(VoteStateVersion.V3);
            state.Votes.Should().Equal(new VoteStateLockout(17, 4, 5));
            state.RootSlot.Should().Be(6);
            state.Commission.Should().Be(3);
            state.PriorVoters.Should().HaveCount(32);
            state.PriorVoterIndex.Should().Be(31);
            state.PriorVotersEmpty.Should().BeFalse();
            state.IsUninitialized.Should().BeFalse();
            VoteStateV3.DataLength.Should().Be(3_762);
        }

        [Test]
        public void PinnedV4BincodeVector_DecodesCollectorsCommissionsBlsAndSharedTail()
        {
            // Arrange
            var data = BuildV4Vector();

            // Act
            var state = VoteStateVersions.Parse(data).Should().BeOfType<VoteStateV4>().Subject;

            // Assert
            state.Version.Should().Be(VoteStateVersion.V4);
            state.Node.ToBytes().Should().OnlyContain(value => value == 21);
            state.AuthorizedWithdrawer.ToBytes().Should().OnlyContain(value => value == 22);
            state.InflationRewardsCollector.ToBytes().Should().OnlyContain(value => value == 23);
            state.BlockRevenueCollector.ToBytes().Should().OnlyContain(value => value == 24);
            state.InflationRewardsCommissionBasisPoints.Should().Be(2_526);
            state.BlockRevenueCommissionBasisPoints.Should().Be(2_728);
            state.PendingDelegatorRewards.Should().Be(29);
            state.BlsPublicKey.Should().NotBeNull();
            state.BlsPublicKey!.Value.ToArray().Should().OnlyContain(value => value == 30);
            state.Votes.Should().Equal(new VoteStateLockout(31, 32, 33));
            state.RootSlot.Should().Be(34);
            state.AuthorizedVoters.Should().Equal(
                new AuthorizedVoteVoter(35, new PublicKey(Enumerable.Repeat((byte)36, 32).ToArray())));
            state.EpochCredits.Should().Equal(new VoteEpochCredits(37, 38, 39));
            state.LastTimestamp.Should().Be(new VoteStateTimestamp(40, -41));
            state.IsUninitialized.Should().BeFalse();
            VoteStateV4.DataLength.Should().Be(3_762);
        }

        [TestCase(0u)]
        [TestCase(4u)]
        [TestCase(uint.MaxValue)]
        public void UnsupportedRawTag_IsRejected(uint tag)
        {
            // Arrange
            var data = new byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32LittleEndian(data, tag);

            // Act & Assert
            FluentActions.Invoking(() => VoteStateVersions.Parse(data))
                .Should().Throw<ArgumentException>().WithMessage($"*{tag}*");
        }

        [Test]
        public void HostileVoteCountAboveTowerBound_IsRejectedBeforeAllocation()
        {
            // Arrange
            using var stream = new MemoryStream();
            WriteUInt32(stream, (uint)VoteStateVersion.V3);
            WriteRepeated(stream, 0, 64);
            stream.WriteByte(0);
            WriteUInt64(stream, VoteStateVersions.MaximumLockouts + 1UL);

            // Act & Assert
            FluentActions.Invoking(() => VoteStateVersions.Parse(stream.ToArray()))
                .Should().Throw<ArgumentException>().WithMessage("*exceeds the maximum*");
        }

        [Test]
        public void NonCanonicalV4BlsOption_IsRejected()
        {
            // Arrange
            var data = BuildV4Vector();
            data[4 + (4 * 32) + (2 * 2) + 8] = 2;

            // Act & Assert
            FluentActions.Invoking(() => VoteStateVersions.Parse(data))
                .Should().Throw<ArgumentException>().WithMessage("*BLS*");
        }
    }

    [TestFixture]
    public sealed class IsCorrectSizeAndInitialized
    {
        [Test]
        public void ExactPinnedAllocations_ApplyVariantInitializationSentinels()
        {
            // Arrange
            var v1 = new byte[VoteStateV1_14_11.DataLength];
            v1[4] = 1;
            var v3 = new byte[VoteStateV3.DataLength];
            v3[4] = 1;
            var v4 = new byte[VoteStateV4.DataLength];
            v4[0] = 3;

            // Act
            var v1Result = VoteStateV1_14_11.IsCorrectSizeAndInitialized(v1);
            var v3Result = VoteStateV3.IsCorrectSizeAndInitialized(v3);
            var v4Result = VoteStateV4.IsCorrectSizeAndInitialized(v4);

            // Assert
            v1Result.Should().BeTrue();
            v3Result.Should().BeTrue();
            v4Result.Should().BeTrue();
            VoteStateVersions.IsCorrectSizeAndInitialized(v1).Should().BeTrue();
            VoteStateVersions.IsCorrectSizeAndInitialized(v3).Should().BeTrue();
            VoteStateVersions.IsCorrectSizeAndInitialized(v4).Should().BeTrue();
            VoteStateVersions.IsCorrectSizeAndInitialized(v4.AsSpan(0, v4.Length - 1)).Should().BeFalse();
        }
    }
}
