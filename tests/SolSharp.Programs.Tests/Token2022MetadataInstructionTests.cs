using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs.Tests;

public static class Token2022MetadataInstructionTests
{
    private static PublicKey Key(byte value) => new([.. Enumerable.Repeat(value, PublicKey.Length)]);

    private static string Hex(Instruction instruction) => Convert.ToHexString(instruction.Data).ToLowerInvariant();

    private static (PublicKey, bool, bool)[] Metas(Instruction instruction)
        => [.. instruction.Accounts.Select(static account => (account.PublicKey, account.IsSigner, account.IsWritable))];

    [TestFixture]
    public sealed class Initialize
    {
        [Test]
        public void MatchesPinnedMetadataInterface()
        {
            // Act
            var instruction = Token2022Program.InitializeTokenMetadata(
                Key(1), Key(2), Key(3), Key(4), "A", "B", "C");

            // Assert
            Hex(instruction).Should().Be(
                "d2e11ea258b84d8d010000004101000000420100000043");
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(2), false, false),
                (Key(3), false, false),
                (Key(4), true, false));
        }
    }

    [TestFixture]
    public sealed class UpdateField
    {
        [Test]
        public void RequiredAndCustomFieldsMatchPinnedMetadataInterface()
        {
            // Act
            var required = Token2022Program.UpdateTokenMetadataField(Key(1), Key(2), TokenMetadataField.Uri, "U");
            var custom = Token2022Program.UpdateTokenMetadataField(Key(1), Key(2), "K", "V");

            // Assert
            Hex(required).Should().Be("dde9312db5cadcc8020100000055");
            Hex(custom).Should().Be("dde9312db5cadcc803010000004b0100000056");
            Metas(custom).Should().Equal((Key(1), false, true), (Key(2), true, false));
        }

        [Test]
        public void InvalidUnicodeIsRejected()
        {
            // Act
            Action act = static () => _ = Token2022Program.UpdateTokenMetadataField(
                Key(1), Key(2), TokenMetadataField.Name, "\ud800");

            // Assert
            act.Should().Throw<ArgumentException>();
        }
    }

    [TestFixture]
    public sealed class RemoveKey
    {
        [Test]
        public void MatchesPinnedMetadataInterface()
        {
            // Act
            var instruction = Token2022Program.RemoveTokenMetadataKey(Key(1), Key(2), "K", idempotent: true);

            // Assert
            Hex(instruction).Should().Be("ea122038598d25b501010000004b");
        }
    }

    [TestFixture]
    public sealed class UpdateAuthority
    {
        [Test]
        public void NullAuthorityMatchesPinnedMetadataInterface()
        {
            // Act
            var instruction = Token2022Program.UpdateTokenMetadataAuthority(Key(1), Key(2), null);

            // Assert
            Hex(instruction).Should().Be("d7e4a6e45464567b" + new string('0', PublicKey.Length * 2));
        }
    }

    [TestFixture]
    public sealed class Emit
    {
        [Test]
        public void RangeMatchesPinnedMetadataInterface()
        {
            // Act
            var instruction = Token2022Program.EmitTokenMetadata(Key(1), 2, 10);

            // Assert
            Hex(instruction).Should().Be("faa6b4fa0d0cb846010200000000000000010a00000000000000");
            Metas(instruction).Should().Equal((Key(1), false, false));
        }
    }
}
