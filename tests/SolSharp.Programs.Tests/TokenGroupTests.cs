using System.Buffers.Binary;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;
using static SolSharp.Programs.Tests.TokenGroupTestHelpers;

namespace SolSharp.Programs.Tests;

internal static class TokenGroupTestHelpers
{
    internal static PublicKey Key(byte value) => new(Enumerable.Repeat(value, PublicKey.Length).ToArray());

    internal static string Hex(Instruction instruction) => Convert.ToHexString(instruction.Data).ToLowerInvariant();

    internal static (PublicKey, bool, bool)[] Metas(Instruction instruction)
        => [.. instruction.Accounts.Select(account => (account.PublicKey, account.IsSigner, account.IsWritable))];

    internal static byte[] GroupData()
    {
        var data = new byte[TokenGroupState.Length];
        Key(1).CopyTo(data);
        Key(2).CopyTo(data.AsSpan(PublicKey.Length));
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(PublicKey.Length * 2), 3UL);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan((PublicKey.Length * 2) + sizeof(ulong)), 4UL);
        return data;
    }

    internal static byte[] MemberData()
    {
        var data = new byte[TokenGroupMemberState.Length];
        Key(5).CopyTo(data);
        Key(2).CopyTo(data.AsSpan(PublicKey.Length));
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(PublicKey.Length * 2), 6UL);
        return data;
    }
}

public static class TokenGroupTests
{
    [TestFixture]
    public sealed class InitializeTokenGroup
    {
        [Test]
        public void MatchesPinnedTokenGroupInterface()
        {
            // Act
            var instruction = Token2022Program.InitializeTokenGroup(Key(1), Key(2), Key(3), Key(4), 100);

            // Assert: the discriminator is a SHA-256 prefix pinned by spl-token-group-interface 0.7.2.
            Hex(instruction).Should().Be(
                "79716c2736330004" +
                "0404040404040404040404040404040404040404040404040404040404040404" +
                "6400000000000000");
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(2), false, false),
                (Key(3), true, false));
        }

        [Test]
        public void MaybeNullRejectsAmbiguousZeroAddress()
        {
            // Act
            Action act = () => _ = Token2022Program.InitializeTokenGroup(
                Key(1), Key(2), Key(3), default(PublicKey), 4);

            // Assert
            act.Should().Throw<ArgumentException>().WithParameterName("updateAuthority");
        }
    }

    [TestFixture]
    public sealed class UpdateTokenGroupMaxSize
    {
        [Test]
        public void MatchesPinnedTokenGroupInterface() =>
            Hex(Token2022Program.UpdateTokenGroupMaxSize(Key(1), Key(4), 200))
                .Should().Be("6c25ab8ff81e126ec800000000000000");

        [Test]
        public void SupportsAnotherInterfaceImplementation()
        {
            // Act
            var instruction = Token2022Program.UpdateTokenGroupMaxSize(Key(1), Key(2), 3, Key(9));

            // Assert
            instruction.ProgramId.Should().Be(Key(9));
        }
    }

    [TestFixture]
    public sealed class UpdateTokenGroupAuthority
    {
        [Test]
        public void MatchesPinnedTokenGroupInterface() =>
            Hex(Token2022Program.UpdateTokenGroupAuthority(Key(1), Key(4), null))
                .Should().Be("a1695801edddd8cb" + new string('0', PublicKey.Length * 2));
    }

    [TestFixture]
    public sealed class InitializeTokenGroupMember
    {
        [Test]
        public void MatchesPinnedTokenGroupInterface()
        {
            // Act
            var instruction = Token2022Program.InitializeTokenGroupMember(Key(5), Key(6), Key(7), Key(1), Key(4));

            // Assert
            Hex(instruction).Should().Be("9820deb0dfed7486");
            Metas(instruction).Should().Equal(
                (Key(5), false, true),
                (Key(6), false, false),
                (Key(7), true, false),
                (Key(1), false, true),
                (Key(4), true, false));
        }
    }
}

public static class TokenGroupStateTests
{
    [TestFixture]
    public sealed class Decode
    {
        [Test]
        public void MatchesPinnedPodLayout()
        {
            // Arrange
            var data = GroupData();

            // Act
            var group = TokenGroupState.Decode(data);

            // Assert
            group.Should().NotBeNull();
            group!.UpdateAuthority.Should().Be(Key(1));
            group.Mint.Should().Be(Key(2));
            group.Size.Should().Be(3);
            group.MaximumSize.Should().Be(4);
            TokenGroupState.Decode(data.AsSpan()[..^1]).Should().BeNull();
        }
    }
}

public static class TokenGroupMemberStateTests
{
    [TestFixture]
    public sealed class Decode
    {
        [Test]
        public void MatchesPinnedPodLayout()
        {
            // Arrange
            var data = MemberData();

            // Act
            var member = TokenGroupMemberState.Decode(data);

            // Assert
            member.Should().NotBeNull();
            member!.Mint.Should().Be(Key(5));
            member.Group.Should().Be(Key(2));
            member.MemberNumber.Should().Be(6);
            TokenGroupMemberState.Decode(data.AsSpan()[..^1]).Should().BeNull();
        }
    }
}
