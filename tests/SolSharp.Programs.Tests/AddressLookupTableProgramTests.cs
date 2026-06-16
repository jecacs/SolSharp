using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs.Tests;

public static class AddressLookupTableProgramTests
{
    private static PublicKey Pk(byte value)
    {
        var bytes = new byte[PublicKey.Length];
        Array.Fill(bytes, value);
        return new PublicKey(bytes);
    }

    private static string DataHex(Instruction instruction) => Convert.ToHexString(instruction.Data).ToLowerInvariant();

    [TestFixture]
    public sealed class CreateLookupTable
    {
        [Test]
        public void DerivesTableAddress_MatchesSolders_AndEncodesData()
        {
            var (instruction, lookupTable) = AddressLookupTableProgram.CreateLookupTable(Pk(1), Pk(2), 123);

            // derive_lookup_table_address(authority=[1]*32, recent_slot=123) from solders: bump 255.
            lookupTable.Should().Be(PublicKey.Parse("2KUourxM9uBoUQRxhbEPBsjoeFkRtjymMhZoHftxifMh"));
            // discriminant 0, recent_slot 123 (u64 LE), bump 255.
            DataHex(instruction).Should().Be("000000007b00000000000000ff");

            instruction.ProgramId.Should().Be(AddressLookupTableProgram.ProgramId);
            instruction.Accounts.Should().HaveCount(4);
            instruction.Accounts[0].PublicKey.Should().Be(lookupTable);
            instruction.Accounts[0].IsWritable.Should().BeTrue();
            instruction.Accounts[0].IsSigner.Should().BeFalse();
            instruction.Accounts[1].PublicKey.Should().Be(Pk(1));
            instruction.Accounts[1].IsSigner.Should().BeTrue();
            instruction.Accounts[1].IsWritable.Should().BeFalse();
            instruction.Accounts[2].PublicKey.Should().Be(Pk(2));
            instruction.Accounts[2].IsSigner.Should().BeTrue();
            instruction.Accounts[2].IsWritable.Should().BeTrue();
            instruction.Accounts[3].PublicKey.Should().Be(SystemProgram.ProgramId);
        }
    }

    [TestFixture]
    public sealed class ExtendLookupTable
    {
        [Test]
        public void EncodesAddresses_AndIncludesPayerAccounts()
        {
            var instruction = AddressLookupTableProgram.ExtendLookupTable(Pk(5), Pk(1), Pk(2), [Pk(3), Pk(4)]);

            // discriminant 2, u64 count 2, then [3]*32 and [4]*32.
            const string expected =
                "020000000200000000000000" +
                "0303030303030303030303030303030303030303030303030303030303030303" +
                "0404040404040404040404040404040404040404040404040404040404040404";
            DataHex(instruction).Should().Be(expected);

            instruction.Accounts.Should().HaveCount(4);
            instruction.Accounts[0].PublicKey.Should().Be(Pk(5));
            instruction.Accounts[0].IsWritable.Should().BeTrue();
            instruction.Accounts[1].PublicKey.Should().Be(Pk(1));
            instruction.Accounts[1].IsSigner.Should().BeTrue();
            instruction.Accounts[2].PublicKey.Should().Be(Pk(2));
            instruction.Accounts[2].IsSigner.Should().BeTrue();
            instruction.Accounts[2].IsWritable.Should().BeTrue();
            instruction.Accounts[3].PublicKey.Should().Be(SystemProgram.ProgramId);
        }

        [Test]
        public void WithoutPayer_OmitsPayerAndSystemAccounts()
        {
            var instruction = AddressLookupTableProgram.ExtendLookupTable(Pk(5), Pk(1), payer: null, [Pk(3)]);

            instruction.Accounts.Should().HaveCount(2);
            instruction.Accounts[0].PublicKey.Should().Be(Pk(5));
            instruction.Accounts[1].PublicKey.Should().Be(Pk(1));
        }
    }

    [TestFixture]
    public sealed class DeactivateLookupTable
    {
        [Test]
        public void EncodesDiscriminator_AndAccounts()
        {
            var instruction = AddressLookupTableProgram.DeactivateLookupTable(Pk(5), Pk(1));

            DataHex(instruction).Should().Be("03000000");
            instruction.Accounts.Should().HaveCount(2);
            instruction.Accounts[0].IsWritable.Should().BeTrue();
            instruction.Accounts[1].IsSigner.Should().BeTrue();
        }
    }

    [TestFixture]
    public sealed class CloseLookupTable
    {
        [Test]
        public void EncodesDiscriminator_AndAccounts()
        {
            var instruction = AddressLookupTableProgram.CloseLookupTable(Pk(5), Pk(1), Pk(6));

            DataHex(instruction).Should().Be("04000000");
            instruction.Accounts.Should().HaveCount(3);
            instruction.Accounts[0].PublicKey.Should().Be(Pk(5));
            instruction.Accounts[1].PublicKey.Should().Be(Pk(1));
            instruction.Accounts[1].IsSigner.Should().BeTrue();
            instruction.Accounts[2].PublicKey.Should().Be(Pk(6));
            instruction.Accounts[2].IsWritable.Should().BeTrue();
        }
    }
}
