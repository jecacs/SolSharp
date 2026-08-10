using System.Buffers.Binary;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;
using SolSharp.Rpc.Models;

namespace SolSharp.Rpc.Tests;

public static class SolanaRpcClientLookupTableTests
{
    private const string TableAddress = "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA";

    // Authoritative ALT account data (verified against solders AddressLookupTable.deserialize): an active
    // table, last_extended_slot 123, authority [9]*32, addresses [2]*32 and [3]*32.
    private const string TableDataBase64 =
        "AQAAAP//////////ewAAAAAAAAAAAQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJAAACAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMD";

    private static (SolanaRpcClient Client, FakeHttpMessageHandler Handler) Make(string responseJson)
    {
        var handler = new FakeHttpMessageHandler(responseJson);
        var http = new HttpClient(handler) { BaseAddress = new("http://localhost") };
        return (new(http), handler);
    }

    private static string AccountEnvelope(
        string dataBase64,
        string owner = SolanaProgramIds.AddressLookupTableProgram,
        ulong contextSlot = 124) =>
        """{"jsonrpc":"2.0","result":{"context":{"slot":__SLOT__},"value":{"data":["__DATA__","base64"],"executable":false,"lamports":1,"owner":"__OWNER__","rentEpoch":0,"space":120}},"id":1}"""
            .Replace("__DATA__", dataBase64)
            .Replace("__OWNER__", owner)
            .Replace("__SLOT__", contextSlot.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static string WithMetadata(
        ulong deactivationSlot = ulong.MaxValue,
        ulong lastExtendedSlot = 123,
        byte lastExtendedSlotStartIndex = 0,
        ushort padding = 0)
    {
        var data = Convert.FromBase64String(TableDataBase64);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(4), deactivationSlot);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(12), lastExtendedSlot);
        data[20] = lastExtendedSlotStartIndex;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(54), padding);
        return Convert.ToBase64String(data);
    }

    [TestFixture]
    public sealed class GetAddressLookupTableAsync
    {
        [Test]
        public async Task DecodesActiveTable()
        {
            // Arrange
            var (client, handler) = Make(AccountEnvelope(TableDataBase64));

            // Act
            var table = await client.GetAddressLookupTableAsync(PublicKey.Parse(TableAddress));

            // Assert
            table.Should().NotBeNull();
            table.IsActive.Should().BeTrue();
            table.IsUsable.Should().BeTrue();
            table.Lifecycle.Should().Be(AddressLookupTableLifecycle.Activated);
            table.DeactivationSlot.Should().Be(ulong.MaxValue);
            table.LastExtendedSlot.Should().Be(123);
            table.LastExtendedSlotStartIndex.Should().Be(0);
            table.ContextSlot.Should().Be(124);
            table.Padding.Should().Be(0);
            table.Authority.Should().Be(PublicKey.Parse("cGfHiC6Kgg3FpFZvgwGcswsCRtp4aBP2fzuXRQPizuN"));
            table.Addresses.Should().HaveCount(2);
            table.StoredAddresses.Should().HaveCount(2);
            table.Addresses[0].Should().Be(PublicKey.Parse("8qbHbw2BbbTHBW1sbeqakYXVKRQM8Ne7pLK7m6CVfeR"));
            table.Addresses[1].Should().Be(PublicKey.Parse("CktRuQ2mttgRGkXJtyksdKHjUdc2C4TgDzyB98oEzy8"));

            handler.CapturedRequestBody.Should().Contain("\"getAccountInfo\"");
            handler.CapturedRequestBody.Should().Contain(TableAddress);
        }

        [Test]
        public async Task SameSlotExtension_ExposesOnlyPreviouslyActivePrefix()
        {
            // Arrange: index 1 is the first address appended at slot 123.
            var (client, _) = Make(AccountEnvelope(
                WithMetadata(lastExtendedSlotStartIndex: 1),
                contextSlot: 123));

            // Act
            var table = await client.GetAddressLookupTableAsync(PublicKey.Parse(TableAddress));

            // Assert
            table.Should().NotBeNull();
            table.LastExtendedSlotStartIndex.Should().Be(1);
            table.ContextSlot.Should().Be(123);
            table.StoredAddresses.Should().HaveCount(2);
            table.Addresses.Should().ContainSingle()
                .Which.Should().Be(PublicKey.Parse("8qbHbw2BbbTHBW1sbeqakYXVKRQM8Ne7pLK7m6CVfeR"));
        }

        [Test]
        public async Task DeactivationStart_RemainsUsableDuringCooldown()
        {
            // Arrange
            const ulong deactivationSlot = 200;
            var (client, _) = Make(AccountEnvelope(
                WithMetadata(deactivationSlot: deactivationSlot, padding: 0x1234),
                contextSlot: deactivationSlot));

            // Act
            var table = await client.GetAddressLookupTableAsync(PublicKey.Parse(TableAddress));

            // Assert
            table.Should().NotBeNull();
            table.IsActive.Should().BeFalse("deactivation has begun");
            table.Lifecycle.Should().Be(AddressLookupTableLifecycle.Deactivating);
            table.IsUsable.Should().BeTrue("Agave permits lookups during the SlotHashes cooldown");
            table.DeactivationSlot.Should().Be(deactivationSlot);
            table.Padding.Should().Be(0x1234);
            table.Addresses.Should().HaveCount(2);
        }

        [Test]
        public async Task LastGuaranteedCooldownSlot_RemainsUsable()
        {
            // Arrange: with no skipped blocks, the deactivation slot is still the oldest of the 512
            // SlotHashes entries at D + 512 (position 511), so Agave still permits lookups.
            var (client, _) = Make(AccountEnvelope(
                WithMetadata(deactivationSlot: 100),
                contextSlot: 612));

            // Act
            var table = await client.GetAddressLookupTableAsync(PublicKey.Parse(TableAddress));

            // Assert
            table.Should().NotBeNull();
            table.IsActive.Should().BeFalse();
            table.Lifecycle.Should().Be(AddressLookupTableLifecycle.Deactivating);
            table.IsUsable.Should().BeTrue();
            table.DeactivationSlot.Should().Be(100);
        }

        [Test]
        public async Task BeyondGuaranteedCooldown_DoesNotGuessWithoutSlotHashes()
        {
            // Arrange: after D + 512, skipped blocks may still retain the deactivation slot, so the
            // account response alone cannot prove whether the table is cooling down or deactivated.
            var (client, _) = Make(AccountEnvelope(
                WithMetadata(deactivationSlot: 100),
                contextSlot: 613));

            // Act
            var table = await client.GetAddressLookupTableAsync(PublicKey.Parse(TableAddress));

            // Assert
            table.Should().NotBeNull();
            table.IsActive.Should().BeFalse();
            table.Lifecycle.Should().Be(AddressLookupTableLifecycle.DeactivationStatusUnknown);
            table.IsUsable.Should().BeNull();
            table.DeactivationSlot.Should().Be(100);
        }

        [Test]
        public async Task ReturnsNullWhenAccountMissing()
        {
            // Arrange
            var (client, _) = Make("""{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":null},"id":1}""");

            // Act
            var table = await client.GetAddressLookupTableAsync(PublicKey.Parse(TableAddress));

            // Assert
            table.Should().BeNull();
        }

        [Test]
        public async Task ReturnsNullWhenDataIsNotALookupTable()
        {
            // Four zero bytes: shorter than the metadata and discriminant 0, so not an initialized table.
            // Arrange
            var (client, _) = Make(AccountEnvelope("AAAAAA=="));

            // Act
            var table = await client.GetAddressLookupTableAsync(PublicKey.Parse(TableAddress));

            // Assert
            table.Should().BeNull();
        }

        [Test]
        public async Task WrongOwner_ReturnsNull()
        {
            // Arrange
            var (client, _) = Make(AccountEnvelope(TableDataBase64, SolanaProgramIds.SystemProgram));

            // Act
            var table = await client.GetAddressLookupTableAsync(PublicKey.Parse(TableAddress));

            // Assert
            table.Should().BeNull();
        }
    }

    [TestFixture]
    public sealed class Decode
    {
        [Test]
        public void InvalidAuthorityOptionOrUnalignedTail_ReturnsNull()
        {
            // Arrange
            var invalidOption = Convert.FromBase64String(TableDataBase64);
            invalidOption[21] = 2;
            byte[] unalignedTail = [.. Convert.FromBase64String(TableDataBase64), 0];

            // Act & Assert
            AddressLookupTable.Decode(invalidOption).Should().BeNull();
            AddressLookupTable.Decode(unalignedTail).Should().BeNull();
        }

        [Test]
        public void InvalidStartIndexOrAddressCount_ReturnsNull()
        {
            // Arrange
            var invalidStartIndex = Convert.FromBase64String(WithMetadata(lastExtendedSlotStartIndex: 3));
            var tooManyAddresses = new byte[56 + (257 * PublicKey.Length)];
            BinaryPrimitives.WriteUInt32LittleEndian(tooManyAddresses, 1);
            BinaryPrimitives.WriteUInt64LittleEndian(tooManyAddresses.AsSpan(4), ulong.MaxValue);

            // Act & Assert
            AddressLookupTable.Decode(invalidStartIndex, 123).Should().BeNull();
            AddressLookupTable.Decode(tooManyAddresses).Should().BeNull();
        }
    }
}
