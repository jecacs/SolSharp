using System.Buffers.Binary;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;
using SolSharp.Core.SysvarStates;

namespace SolSharp.Programs.Tests;

public static class AddressLookupTableStateTests
{
    private static PublicKey Pk(byte value) => new([.. Enumerable.Repeat(value, PublicKey.Length)]);

    private static AddressLookupTableState Table(
        ulong deactivationSlot = ulong.MaxValue,
        ulong lastExtendedSlot = 10,
        byte lastExtendedSlotStartIndex = 1,
        int addressCount = 3)
    {
        var data = new byte[AddressLookupTableState.MetadataLength + (addressCount * PublicKey.Length)];
        BinaryPrimitives.WriteUInt32LittleEndian(data, 1);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(4), deactivationSlot);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(12), lastExtendedSlot);
        data[20] = lastExtendedSlotStartIndex;
        data[21] = 0;
        for (var i = 0; i < addressCount; i++)
            Pk((byte)(i + 1)).CopyTo(data.AsSpan(AddressLookupTableState.MetadataLength + (i * PublicKey.Length)));
        return AddressLookupTableState.Parse(data);
    }

    private static SlotHashesSysvarState SlotHashes(params ulong[] slots)
    {
        var data = new byte[sizeof(ulong) + (slots.Length * (sizeof(ulong) + Hash.Length))];
        BinaryPrimitives.WriteUInt64LittleEndian(data, checked((ulong)slots.Length));
        var offset = sizeof(ulong);
        foreach (var slot in slots)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(offset), slot);
            offset += sizeof(ulong) + Hash.Length;
        }

        return SlotHashesSysvarState.Parse(data);
    }

    [TestFixture]
    public sealed class Parse
    {
        [Test]
        public void InitializedTable_DecodesPinned56ByteMetadataLayoutAndAddresses()
        {
            // Arrange
            var data = new byte[AddressLookupTableState.MetadataLength + (2 * PublicKey.Length)];
            BinaryPrimitives.WriteUInt32LittleEndian(data, 1);
            BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(4), 11);
            BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(12), 12);
            data[20] = 1;
            data[21] = 1;
            Pk(3).CopyTo(data.AsSpan(22));
            Pk(4).CopyTo(data.AsSpan(56));
            Pk(5).CopyTo(data.AsSpan(88));

            // Act
            var state = AddressLookupTableState.Parse(data);

            // Assert
            state.Kind.Should().Be(AddressLookupTableStateKind.LookupTable);
            state.DeactivationSlot.Should().Be(11);
            state.LastExtendedSlot.Should().Be(12);
            state.LastExtendedSlotStartIndex.Should().Be(1);
            state.Authority.Should().Be(Pk(3));
            state.Addresses.Should().Equal(Pk(4), Pk(5));
        }

        [Test]
        public void FrozenTable_DecodesNoneAuthority_AndRejectsMisalignedAddresses()
        {
            // Arrange
            var frozen = new byte[AddressLookupTableState.MetadataLength];
            BinaryPrimitives.WriteUInt32LittleEndian(frozen, 1);
            frozen[21] = 0;
            var misaligned = new byte[AddressLookupTableState.MetadataLength + 1];
            frozen.CopyTo(misaligned, 0);

            // Act
            var state = AddressLookupTableState.Parse(frozen);
            Action act = () => AddressLookupTableState.Parse(misaligned);

            // Assert
            state.Authority.Should().BeNull();
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void MalformedMetadata_IsRejectedWithTheDocumentedExceptionTypes()
        {
            // Arrange
            var truncated = new byte[AddressLookupTableState.MetadataLength - 1];
            BinaryPrimitives.WriteUInt32LittleEndian(truncated, 1);
            var unknownDiscriminator = new byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32LittleEndian(unknownDiscriminator, 2);
            var invalidAuthority = new byte[AddressLookupTableState.MetadataLength];
            BinaryPrimitives.WriteUInt32LittleEndian(invalidAuthority, 1);
            invalidAuthority[21] = 2;
            var tooManyAddresses = new byte[
                AddressLookupTableState.MetadataLength +
                ((AddressLookupTableState.MaximumAddresses + 1) * PublicKey.Length)];
            BinaryPrimitives.WriteUInt32LittleEndian(tooManyAddresses, 1);

            // Act
            Action parseTruncated = () => _ = AddressLookupTableState.Parse(truncated);
            Action parseUnknown = () => _ = AddressLookupTableState.Parse(unknownDiscriminator);
            Action parseInvalidAuthority = () => _ = AddressLookupTableState.Parse(invalidAuthority);
            Action parseTooManyAddresses = () => _ = AddressLookupTableState.Parse(tooManyAddresses);

            // Assert
            parseTruncated.Should().Throw<ArgumentException>();
            parseUnknown.Should().Throw<FormatException>();
            parseInvalidAuthority.Should().Throw<FormatException>();
            parseTooManyAddresses.Should().Throw<ArgumentException>();
        }
    }

    [TestFixture]
    public sealed class TryParse
    {
        [Test]
        public void ValidAndMalformedStates_ReturnSuccessAndFailureWithoutThrowing()
        {
            // Arrange
            var valid = new byte[AddressLookupTableState.MetadataLength];
            BinaryPrimitives.WriteUInt32LittleEndian(valid, 1);
            var malformed = new byte[1];

            // Act
            var validResult = AddressLookupTableState.TryParse(valid, out var state);
            var malformedResult = AddressLookupTableState.TryParse(malformed, out var malformedState);

            // Assert
            validResult.Should().BeTrue();
            state.Should().NotBeNull();
            state.Kind.Should().Be(AddressLookupTableStateKind.LookupTable);
            malformedResult.Should().BeFalse();
            malformedState.Should().BeNull();
        }
    }

    [TestFixture]
    public sealed class EstimateLastValidSlot
    {
        [Test]
        public void NormalAndOverflowingSlots_MatchSaturatingUpstreamEstimate()
        {
            // Act & Assert
            AddressLookupTableState.EstimateLastValidSlot(1_000).Should().Be(1_512);
            AddressLookupTableState.EstimateLastValidSlot(ulong.MaxValue - 10).Should().Be(ulong.MaxValue);
        }
    }

    [TestFixture]
    public sealed class GetStatus
    {
        [Test]
        public void ActivationBranches_MatchSlotHashesPositionSemantics()
        {
            // Arrange
            var slotHashes = SlotHashes(90, 80, 70);

            // Act
            var activated = Table().GetStatus(100, slotHashes);
            var justDeactivating = Table(deactivationSlot: 100).GetStatus(100, slotHashes);
            var coolingDown = Table(deactivationSlot: 80).GetStatus(100, slotHashes);
            var deactivated = Table(deactivationSlot: 60).GetStatus(100, slotHashes);

            // Assert
            activated.Should().Be(new AddressLookupTableStatus(AddressLookupTableStatusKind.Activated));
            justDeactivating.Should().Be(new AddressLookupTableStatus(
                AddressLookupTableStatusKind.Deactivating,
                SlotHashesSysvarState.MaximumEntries + 1));
            coolingDown.Should().Be(new AddressLookupTableStatus(
                AddressLookupTableStatusKind.Deactivating,
                SlotHashesSysvarState.MaximumEntries - 1));
            deactivated.Should().Be(new AddressLookupTableStatus(AddressLookupTableStatusKind.Deactivated));
        }

        [Test]
        public void OldestRetainedAndEvictedSlots_MatchTheFullRuntimeCapacityBoundary()
        {
            // Arrange
            var retainedSlots = Enumerable.Range(1, SlotHashesSysvarState.MaximumEntries)
                .Reverse()
                .Select(static slot => (ulong)slot)
                .ToArray();
            var afterEviction = Enumerable.Range(2, SlotHashesSysvarState.MaximumEntries)
                .Reverse()
                .Select(static slot => (ulong)slot)
                .ToArray();
            var table = Table(deactivationSlot: 1);

            // Act
            var oldestRetained = table.GetStatus(600, SlotHashes(retainedSlots));
            var evicted = table.GetStatus(600, SlotHashes(afterEviction));

            // Assert
            oldestRetained.Should().Be(new AddressLookupTableStatus(
                AddressLookupTableStatusKind.Deactivating,
                RemainingBlocks: 1));
            evicted.Should().Be(new AddressLookupTableStatus(AddressLookupTableStatusKind.Deactivated));
        }

        [Test]
        public void UninitializedState_Throws()
        {
            // Arrange
            var state = AddressLookupTableState.Parse(new byte[sizeof(uint)]);

            // Act
            Action act = () => _ = state.GetStatus(1, SlotHashes());

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }
    }

    [TestFixture]
    public sealed class IsActive
    {
        [Test]
        public void CoolingDownIsUsable_ButExpiredIsNot()
        {
            // Arrange
            var slotHashes = SlotHashes(90, 80, 70);

            // Act & Assert
            Table(deactivationSlot: 80).IsActive(100, slotHashes).Should().BeTrue();
            Table(deactivationSlot: 60).IsActive(100, slotHashes).Should().BeFalse();
        }
    }

    [TestFixture]
    public sealed class GetActiveAddressesLength
    {
        [Test]
        public void SameSlotUsesPreExtensionPrefix_AndLaterSlotUsesAllAddresses()
        {
            // Arrange
            var table = Table(lastExtendedSlot: 10, lastExtendedSlotStartIndex: 1);
            var slotHashes = SlotHashes();

            // Act & Assert
            table.GetActiveAddressesLength(10, slotHashes).Should().Be(1);
            table.GetActiveAddressesLength(11, slotHashes).Should().Be(3);
        }

        [Test]
        public void DeactivatedTable_Throws()
        {
            // Arrange
            var table = Table(deactivationSlot: 5);

            // Act
            Action act = () => _ = table.GetActiveAddressesLength(100, SlotHashes());

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }
    }

    [TestFixture]
    public sealed class GetActiveAddresses
    {
        [Test]
        public void ReturnsDefensiveCopyOfVisiblePrefix()
        {
            // Arrange
            var table = Table(lastExtendedSlotStartIndex: 1);

            // Act
            var addresses = table.GetActiveAddresses(10, SlotHashes());
            addresses[0] = Pk(99);

            // Assert
            table.GetActiveAddresses(10, SlotHashes()).Should().Equal(Pk(1));
        }

        [Test]
        public void MalformedPrefix_Throws()
        {
            // Arrange
            var table = Table(lastExtendedSlotStartIndex: 4, addressCount: 3);

            // Act
            Action act = () => _ = table.GetActiveAddresses(10, SlotHashes());

            // Assert
            act.Should().Throw<FormatException>();
        }
    }

    [TestFixture]
    public sealed class Lookup
    {
        [Test]
        public void ActiveIndexes_PreserveCallerOrder()
        {
            // Arrange
            var table = Table();

            // Act
            var addresses = table.Lookup(11, [2, 0], SlotHashes());

            // Assert
            addresses.Should().Equal(Pk(3), Pk(1));
        }

        [Test]
        public void SameSlotHiddenIndex_Throws()
        {
            // Arrange
            var table = Table(lastExtendedSlotStartIndex: 1);

            // Act
            Action act = () => _ = table.Lookup(10, [1], SlotHashes());

            // Assert
            act.Should().Throw<FormatException>();
        }

        [Test]
        public void DeactivatingTable_RemainsUsableDuringCooldown()
        {
            // Arrange
            var table = Table(deactivationSlot: 100);

            // Act
            var addresses = table.Lookup(100, [2, 0], SlotHashes());

            // Assert
            addresses.Should().Equal(Pk(3), Pk(1));
        }
    }
}
