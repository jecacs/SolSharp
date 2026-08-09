using System.Buffers.Binary;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;
using static SolSharp.Programs.Tests.StakeProgramTestHelpers;

namespace SolSharp.Programs.Tests;

internal static class StakeProgramTestHelpers
{
    internal static PublicKey Pk(byte value) => new(Enumerable.Repeat(value, PublicKey.Length).ToArray());

    internal static string Hex(Instruction instruction) => Convert.ToHexString(instruction.Data).ToLowerInvariant();

    internal static (PublicKey, bool, bool)[] Metas(Instruction instruction)
        => [.. instruction.Accounts.Select(account => (account.PublicKey, account.IsSigner, account.IsWritable))];
}

public static class StakeProgramTests
{
    [TestFixture]
    public sealed class CreateAccount
    {
        [Test]
        public void ComposesSystemCreateAndInitialize()
        {
            // Arrange
            const ulong lamports = 123;
            var authorized = new StakeAuthorized(Pk(3), Pk(4));
            var lockup = new StakeLockup(-2, 7, Pk(5));
            var expectedCreate = SystemProgram.CreateAccount(
                Pk(1), Pk(2), lamports, StakeProgram.AccountDataLength, StakeProgram.ProgramId);
            var expectedInitialize = StakeProgram.Initialize(Pk(2), authorized, lockup);

            // Act
            var instructions = StakeProgram.CreateAccount(Pk(1), Pk(2), authorized, lockup, lamports);

            // Assert
            instructions.Should().HaveCount(2);
            instructions[0].Should().BeEquivalentTo(expectedCreate);
            instructions[1].Should().BeEquivalentTo(expectedInitialize);
        }
    }

    [TestFixture]
    public sealed class CreateAccountChecked
    {
        [Test]
        public void ComposesSystemCreateAndCheckedInitialize()
        {
            // Arrange
            const ulong lamports = 123;
            var authorized = new StakeAuthorized(Pk(3), Pk(4));
            var expectedCreate = SystemProgram.CreateAccount(
                Pk(1), Pk(2), lamports, StakeProgram.AccountDataLength, StakeProgram.ProgramId);
            var expectedInitialize = StakeProgram.InitializeChecked(Pk(2), authorized);

            // Act
            var instructions = StakeProgram.CreateAccountChecked(Pk(1), Pk(2), authorized, lamports);

            // Assert
            instructions.Should().HaveCount(2);
            instructions[0].Should().BeEquivalentTo(expectedCreate);
            instructions[1].Should().BeEquivalentTo(expectedInitialize);
        }
    }

    [TestFixture]
    public sealed class CreateAccountWithSeed
    {
        [Test]
        public void ComposesSystemCreateWithSeedAndInitialize()
        {
            // Arrange
            const ulong lamports = 123;
            const string seed = "stake-seed";
            var authorized = new StakeAuthorized(Pk(4), Pk(5));
            var lockup = new StakeLockup(-2, 7, Pk(6));
            var expectedCreate = SystemProgram.CreateAccountWithSeed(
                Pk(1),
                Pk(2),
                Pk(3),
                seed,
                lamports,
                StakeProgram.AccountDataLength,
                StakeProgram.ProgramId);
            var expectedInitialize = StakeProgram.Initialize(Pk(2), authorized, lockup);

            // Act
            var instructions = StakeProgram.CreateAccountWithSeed(
                Pk(1), Pk(2), Pk(3), seed, authorized, lockup, lamports);

            // Assert
            instructions.Should().HaveCount(2);
            instructions[0].Should().BeEquivalentTo(expectedCreate);
            instructions[1].Should().BeEquivalentTo(expectedInitialize);
        }
    }

    [TestFixture]
    public sealed class CreateAccountWithSeedChecked
    {
        [Test]
        public void ComposesSystemCreateWithSeedAndCheckedInitialize()
        {
            // Arrange
            const ulong lamports = 123;
            const string seed = "stake-seed";
            var authorized = new StakeAuthorized(Pk(4), Pk(5));
            var expectedCreate = SystemProgram.CreateAccountWithSeed(
                Pk(1),
                Pk(2),
                Pk(3),
                seed,
                lamports,
                StakeProgram.AccountDataLength,
                StakeProgram.ProgramId);
            var expectedInitialize = StakeProgram.InitializeChecked(Pk(2), authorized);

            // Act
            var instructions = StakeProgram.CreateAccountWithSeedChecked(
                Pk(1), Pk(2), Pk(3), seed, authorized, lamports);

            // Assert
            instructions.Should().HaveCount(2);
            instructions[0].Should().BeEquivalentTo(expectedCreate);
            instructions[1].Should().BeEquivalentTo(expectedInitialize);
        }
    }

    [TestFixture]
    public sealed class CreateAccountAndDelegateStake
    {
        [Test]
        public void AppendsDelegateInstructionUsingStakerAuthority()
        {
            // Arrange
            const ulong lamports = 123;
            var authorized = new StakeAuthorized(Pk(3), Pk(4));
            var lockup = new StakeLockup(-2, 7, Pk(5));
            var expectedCreate = StakeProgram.CreateAccount(Pk(1), Pk(2), authorized, lockup, lamports);
            var expectedDelegate = StakeProgram.DelegateStake(Pk(2), authorized.Staker, Pk(6));

            // Act
            var instructions = StakeProgram.CreateAccountAndDelegateStake(
                Pk(1), Pk(2), Pk(6), authorized, lockup, lamports);

            // Assert
            instructions.Should().HaveCount(3);
            instructions[0].Should().BeEquivalentTo(expectedCreate[0]);
            instructions[1].Should().BeEquivalentTo(expectedCreate[1]);
            instructions[2].Should().BeEquivalentTo(expectedDelegate);
        }
    }

    [TestFixture]
    public sealed class CreateAccountWithSeedAndDelegateStake
    {
        [Test]
        public void AppendsDelegateInstructionUsingStakerAuthority()
        {
            // Arrange
            const ulong lamports = 123;
            const string seed = "stake-seed";
            var authorized = new StakeAuthorized(Pk(4), Pk(5));
            var lockup = new StakeLockup(-2, 7, Pk(6));
            var expectedCreate = StakeProgram.CreateAccountWithSeed(
                Pk(1), Pk(2), Pk(3), seed, authorized, lockup, lamports);
            var expectedDelegate = StakeProgram.DelegateStake(Pk(2), authorized.Staker, Pk(7));

            // Act
            var instructions = StakeProgram.CreateAccountWithSeedAndDelegateStake(
                Pk(1), Pk(2), Pk(3), seed, Pk(7), authorized, lockup, lamports);

            // Assert
            instructions.Should().HaveCount(3);
            instructions[0].Should().BeEquivalentTo(expectedCreate[0]);
            instructions[1].Should().BeEquivalentTo(expectedCreate[1]);
            instructions[2].Should().BeEquivalentTo(expectedDelegate);
        }
    }

    [TestFixture]
    public sealed class SplitStake
    {
        [Test]
        public void ComposesAllocateAssignAndNativeSplit()
        {
            // Arrange
            const ulong lamports = 123;
            var expectedAllocate = SystemProgram.Allocate(Pk(3), StakeProgram.AccountDataLength);
            var expectedAssign = SystemProgram.Assign(Pk(3), StakeProgram.ProgramId);
            var expectedSplit = StakeProgram.SplitStakeInstruction(Pk(1), Pk(2), lamports, Pk(3));

            // Act
            var instructions = StakeProgram.SplitStake(Pk(1), Pk(2), lamports, Pk(3));

            // Assert
            instructions.Should().HaveCount(3);
            instructions[0].Should().BeEquivalentTo(expectedAllocate);
            instructions[1].Should().BeEquivalentTo(expectedAssign);
            instructions[2].Should().BeEquivalentTo(expectedSplit);
        }
    }

    [TestFixture]
    public sealed class SplitStakeWithSeed
    {
        [Test]
        public void ComposesAllocateWithSeedAndNativeSplit()
        {
            // Arrange
            const ulong lamports = 123;
            const string seed = "split-seed";
            var expectedAllocate = SystemProgram.AllocateWithSeed(
                Pk(3), Pk(4), seed, StakeProgram.AccountDataLength, StakeProgram.ProgramId);
            var expectedSplit = StakeProgram.SplitStakeInstruction(Pk(1), Pk(2), lamports, Pk(3));

            // Act
            var instructions = StakeProgram.SplitStakeWithSeed(Pk(1), Pk(2), lamports, Pk(3), Pk(4), seed);

            // Assert
            instructions.Should().HaveCount(2);
            instructions[0].Should().BeEquivalentTo(expectedAllocate);
            instructions[1].Should().BeEquivalentTo(expectedSplit);
        }
    }

    [TestFixture]
    public sealed class Initialize
    {
        [Test]
        public void MatchesStakeInterfaceBincodeLayout()
        {
            // Act
            var instruction = StakeProgram.Initialize(
                Pk(9),
                new StakeAuthorized(Pk(1), Pk(2)),
                new StakeLockup(-2, 3, Pk(4)));

            // Assert
            var expected =
                "00000000" +
                string.Concat(Enumerable.Repeat("01", 32)) +
                string.Concat(Enumerable.Repeat("02", 32)) +
                "feffffffffffffff0300000000000000" +
                string.Concat(Enumerable.Repeat("04", 32));
            Hex(instruction).Should().Be(expected);
            instruction.Accounts.Select(account => account.PublicKey).Should().Equal(
                Pk(9),
                PublicKey.Parse("SysvarRent111111111111111111111111111111111"));
        }
    }

    [TestFixture]
    public sealed class Authorize
    {
        [Test]
        public void MatchesPinnedDiscriminatorAndFieldOrder() =>
            Hex(StakeProgram.Authorize(Pk(1), Pk(2), Pk(3), StakeAuthorityType.Withdrawer))
                .Should().Be("01000000" + string.Concat(Enumerable.Repeat("03", 32)) + "01000000");
    }

    [TestFixture]
    public sealed class Withdraw
    {
        [Test]
        public void MatchesPinnedWireAndOptionalCustodianOrdering()
        {
            // Act
            var instruction = StakeProgram.Withdraw(
                Pk(1), Pk(2), Pk(3), 0x0102030405060708, Pk(4));

            // Assert
            Hex(instruction).Should().Be("040000000807060504030201");
            Metas(instruction).Should().Equal(
                (Pk(1), false, true),
                (Pk(3), false, true),
                (PublicKey.Parse(Sysvars.Clock), false, false),
                (PublicKey.Parse(Sysvars.StakeHistory), false, false),
                (Pk(2), true, false),
                (Pk(4), true, false));
        }
    }

    [TestFixture]
    public sealed class SplitStakeInstruction
    {
        [Test]
        public void MatchesPinnedDiscriminatorAndFieldOrder() =>
            Hex(StakeProgram.SplitStakeInstruction(Pk(1), Pk(2), 0x0102030405060708, Pk(3)))
                .Should().Be("030000000807060504030201");
    }

    [TestFixture]
    public sealed class SetLockup
    {
        [Test]
        public void MatchesPinnedDiscriminatorAndFieldOrder() =>
            Hex(StakeProgram.SetLockup(Pk(1), new StakeLockupArguments(-2, null, Pk(4)), Pk(2)))
                .Should().Be(
                    "0600000001feffffffffffffff00" +
                    "01" + string.Concat(Enumerable.Repeat("04", 32)));
    }

    [TestFixture]
    public sealed class AuthorizeWithSeed
    {
        [Test]
        public void MatchesPinnedDiscriminatorAndFieldOrder() =>
            Hex(StakeProgram.AuthorizeWithSeed(
                    Pk(1),
                    Pk(2),
                    "ab",
                    Pk(4),
                    Pk(3),
                    StakeAuthorityType.Staker))
                .Should().Be(
                    "08000000" + string.Concat(Enumerable.Repeat("03", 32)) +
                    "0000000002000000000000006162" + string.Concat(Enumerable.Repeat("04", 32)));
    }

    [TestFixture]
    public sealed class AuthorizeCheckedWithSeed
    {
        [Test]
        public void MatchesPinnedDiscriminatorAndFieldOrder() =>
            Hex(StakeProgram.AuthorizeCheckedWithSeed(
                    Pk(1),
                    Pk(2),
                    "ab",
                    Pk(4),
                    Pk(3),
                    StakeAuthorityType.Withdrawer))
                .Should().Be(
                    "0b0000000100000002000000000000006162" + string.Concat(Enumerable.Repeat("04", 32)));
    }

    [TestFixture]
    public sealed class SetLockupChecked
    {
        [Test]
        public void MatchesPinnedDiscriminatorAndFieldOrder() =>
            Hex(StakeProgram.SetLockupChecked(Pk(1), new StakeLockupArguments(null, 7, Pk(4)), Pk(2)))
                .Should().Be("0c00000000010700000000000000");
    }

    [TestFixture]
    public sealed class MoveStake
    {
        [Test]
        public void MatchesPinnedDiscriminatorAndFieldOrder() =>
            Hex(StakeProgram.MoveStake(Pk(1), Pk(2), Pk(3), 9)).Should().Be("100000000900000000000000");
    }

    [TestFixture]
    public sealed class MoveLamports
    {
        [Test]
        public void MatchesPinnedDiscriminatorAndFieldOrder() =>
            Hex(StakeProgram.MoveLamports(Pk(1), Pk(2), Pk(3), 9)).Should().Be("110000000900000000000000");
    }

    [TestFixture]
    public sealed class DelegateStake
    {
        [Test]
        public void UsesPinnedStableDiscriminator() =>
            Hex(StakeProgram.DelegateStake(Pk(1), Pk(2), Pk(3))).Should().Be("02000000");
    }

    [TestFixture]
    public sealed class Deactivate
    {
        [Test]
        public void UsesPinnedStableDiscriminator() =>
            Hex(StakeProgram.Deactivate(Pk(1), Pk(2))).Should().Be("05000000");
    }

    [TestFixture]
    public sealed class Merge
    {
        [Test]
        public void UsesPinnedStableDiscriminator() =>
            Hex(StakeProgram.Merge(Pk(1), Pk(2), Pk(3))).Should().Be("07000000");
    }

    [TestFixture]
    public sealed class InitializeChecked
    {
        [Test]
        public void UsesPinnedStableDiscriminator() =>
            Hex(StakeProgram.InitializeChecked(Pk(1), new StakeAuthorized(Pk(2), Pk(3))))
                .Should().Be("09000000");
    }

    [TestFixture]
    public sealed class AuthorizeChecked
    {
        [Test]
        public void UsesPinnedStableDiscriminator() =>
            Hex(StakeProgram.AuthorizeChecked(Pk(1), Pk(2), Pk(3), StakeAuthorityType.Staker))
                .Should().Be("0a00000000000000");
    }

    [TestFixture]
    public sealed class GetMinimumDelegation
    {
        [Test]
        public void UsesPinnedStableDiscriminator() =>
            Hex(StakeProgram.GetMinimumDelegation()).Should().Be("0d000000");
    }

    [TestFixture]
    public sealed class DeactivateDelinquent
    {
        [Test]
        public void UsesPinnedStableDiscriminator() =>
            Hex(StakeProgram.DeactivateDelinquent(Pk(1), Pk(2), Pk(3))).Should().Be("0e000000");
    }
}

public static class StakeAuthorizedTests
{
    [TestFixture]
    public sealed class ForSingleAuthority
    {
        [Test]
        public void AssignsSameKeyToBothRoles()
        {
            // Act
            var authorized = StakeAuthorized.ForSingleAuthority(Pk(1));

            // Assert
            authorized.Staker.Should().Be(Pk(1));
            authorized.Withdrawer.Should().Be(Pk(1));
        }
    }
}

public static class StakeAccountStateTests
{
    [TestFixture]
    public sealed class Parse
    {
        [Test]
        public void DelegatedState_DecodesExactStakeStateV2Offsets()
        {
            // Arrange
            var data = new byte[StakeAccountState.AccountDataLength];
            BinaryPrimitives.WriteUInt32LittleEndian(data, 2);
            BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(4), 11);
            Pk(1).CopyTo(data.AsSpan(12));
            Pk(2).CopyTo(data.AsSpan(44));
            BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(76), -3);
            BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(84), 5);
            Pk(3).CopyTo(data.AsSpan(92));
            Pk(4).CopyTo(data.AsSpan(124));
            BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(156), 13);
            BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(164), 17);
            BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(172), ulong.MaxValue);
            BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(180), 19);
            BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(188), 23);
            data[196] = 1;

            // Act
            var state = StakeAccountState.Parse(data);

            // Assert
            state.Kind.Should().Be(StakeAccountStateKind.Stake);
            state.Metadata.Should().Be(new StakeAccountMetadata(
                11,
                new StakeAuthorized(Pk(1), Pk(2)),
                new StakeLockup(-3, 5, Pk(3))));
            state.Stake.Should().Be(new StakeDelegatedData(
                new StakeDelegation(Pk(4), 13, 17, ulong.MaxValue, 19),
                23));
            state.StakeFlags.Should().Be(1);
        }
    }
}
