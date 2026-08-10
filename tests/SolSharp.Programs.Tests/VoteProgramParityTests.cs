using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;
using static SolSharp.Programs.Tests.VoteProgramParityTestHelpers;

namespace SolSharp.Programs.Tests;

public static class VoteProgramParityTests
{
    [TestFixture]
    public sealed class InitializeAccount
    {
        [Test]
        public void MatchesPinnedLegacyFieldOrderAndAccounts()
        {
            // Arrange
            var initialize = new VoteInitialize(Pk(1), Pk(2), Pk(3), 7);

            // Act
            var instruction = VoteProgram.InitializeAccount(Pk(9), initialize);

            // Assert
            Hex(instruction).Should().Be(
                "00000000" + Repeat(1, 32) + Repeat(2, 32) + Repeat(3, 32) + "07");
            Metas(instruction).Should().Equal(
                (Pk(9), false, true),
                (PublicKey.Parse(Sysvars.Rent), false, false),
                (PublicKey.Parse(Sysvars.Clock), false, false),
                (Pk(1), true, false));
        }
    }

    [TestFixture]
    public sealed class CreateAccount
    {
        [Test]
        public void ComposesPinnedSystemCreateAndLegacyInitialize()
        {
            // Arrange
            const ulong lamports = 123;
            var initialize = new VoteInitialize(Pk(1), Pk(2), Pk(3), 7);
            var expectedCreate = SystemProgram.CreateAccount(
                Pk(4), Pk(5), lamports, VoteProgram.AccountDataLength, VoteProgram.ProgramId);
            var expectedInitialize = VoteProgram.InitializeAccount(Pk(5), initialize);

            // Act
            var instructions = VoteProgram.CreateAccount(Pk(4), Pk(5), initialize, lamports);

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
        public void ComposesPinnedSystemCreateWithSeedAndLegacyInitialize()
        {
            // Arrange
            const ulong lamports = 123;
            const string seed = "vote-seed";
            var initialize = new VoteInitialize(Pk(1), Pk(2), Pk(3), 7);
            var expectedCreate = SystemProgram.CreateAccountWithSeed(
                Pk(4),
                Pk(5),
                Pk(6),
                seed,
                lamports,
                VoteProgram.AccountDataLength,
                VoteProgram.ProgramId);
            var expectedInitialize = VoteProgram.InitializeAccount(Pk(5), initialize);

            // Act
            var instructions = VoteProgram.CreateAccountWithSeed(
                Pk(4), Pk(5), Pk(6), seed, initialize, lamports);

            // Assert
            instructions.Should().HaveCount(2);
            instructions[0].Should().BeEquivalentTo(expectedCreate);
            instructions[1].Should().BeEquivalentTo(expectedInitialize);
        }
    }

    [TestFixture]
    public sealed class InitializeAccountV2
    {
        [Test]
        public void MatchesPinnedFieldOrderAndWidths()
        {
            // Arrange
            var initialize = new VoteInitializeV2(
                Pk(1),
                Pk(2),
                [.. Enumerable.Repeat((byte)3, VoteAuthorization.BlsPublicKeyLength)],
                [.. Enumerable.Repeat((byte)4, VoteAuthorization.BlsProofOfPossessionLength)],
                Pk(5),
                0x1234,
                0xabcd);

            // Act
            var instruction = VoteProgram.InitializeAccountV2(Pk(9), initialize, Pk(6), Pk(7));

            // Assert
            Hex(instruction).Should().Be(
                "10000000" + Repeat(1, 32) + Repeat(2, 32) + Repeat(3, 48) + Repeat(4, 96) +
                Repeat(5, 32) + "3412cdab");
            instruction.Accounts.Select(static account => (account.PublicKey, account.IsSigner, account.IsWritable))
                .Should().Equal(
                    (Pk(9), false, true),
                    (Pk(1), true, false),
                    (Pk(6), false, true),
                    (Pk(7), false, true));
        }
    }

    [TestFixture]
    public sealed class Authorize
    {
        [Test]
        public void MatchesBincodeEnumAndStructOrder()
        {
            // Arrange
            var authorization = VoteAuthorization.VoterWithBls(
                [.. Enumerable.Repeat((byte)4, VoteAuthorization.BlsPublicKeyLength)],
                [.. Enumerable.Repeat((byte)5, VoteAuthorization.BlsProofOfPossessionLength)]);

            // Act
            var instruction = VoteProgram.Authorize(Pk(1), Pk(2), Pk(3), authorization);

            // Assert
            Hex(instruction).Should().Be(
                "01000000" + Repeat(3, 32) + "02000000" + Repeat(4, 48) + Repeat(5, 96));
        }
    }

    [TestFixture]
    public sealed class AuthorizeWithSeed
    {
        [Test]
        public void MatchesBincodeEnumAndStructOrder()
            => Hex(VoteProgram.AuthorizeWithSeed(Pk(1), Pk(2), Pk(4), "ab", Pk(3), VoteAuthorization.Withdrawer))
                .Should().Be(
                    "0a00000001000000" + Repeat(4, 32) +
                    "02000000000000006162" + Repeat(3, 32));
    }

    [TestFixture]
    public sealed class UpdateCommissionCollector
    {
        [Test]
        public void MatchesBincodeEnumAndStructOrder()
            => Hex(VoteProgram.UpdateCommissionCollector(Pk(1), Pk(2), Pk(3), VoteCommissionKind.BlockRevenue))
                .Should().Be("1100000001000000");
    }

    [TestFixture]
    public sealed class UpdateCommissionBps
    {
        [Test]
        public void MatchesBincodeEnumAndStructOrder()
            => Hex(VoteProgram.UpdateCommissionBps(Pk(1), Pk(2), VoteCommissionKind.BlockRevenue, 0x1234))
                .Should().Be("12000000341201000000");
    }

    [TestFixture]
    public sealed class DepositDelegatorRewards
    {
        [Test]
        public void MatchesBincodeEnumAndStructOrder()
            => Hex(VoteProgram.DepositDelegatorRewards(Pk(1), Pk(2), 0x0102030405060708))
                .Should().Be("130000000807060504030201");
    }

    [TestFixture]
    public sealed class UpdateVoteState
    {
        [Test]
        public void MatchesBincodeVecDequeAndOptions()
        {
            // Arrange
            var update = Update();

            // Act
            var instruction = VoteProgram.UpdateVoteState(Pk(1), Pk(2), update);

            // Assert
            Hex(instruction).Should().Be(
                "08000000" +
                "0200000000000000" +
                "070000000000000001000000" +
                "2c0100000000000002000000" +
                "010500000000000000" + Repeat(9, 32) +
                "01feffffffffffffff");
        }
    }

    [TestFixture]
    public sealed class CompactUpdateVoteState
    {
        [Test]
        public void MatchesShortVecAndUnsignedLeb128()
        {
            // Act
            var instruction = VoteProgram.CompactUpdateVoteState(Pk(1), Pk(2), Update());

            // Assert
            Hex(instruction).Should().Be("0c000000" + CompactBody());
        }

        [Test]
        public void NonMonotonicSlotsAndWideConfirmationCounts_AreRejected()
        {
            // Arrange
            var descending = new VoteStateUpdate([new(9, 1), new(8, 1)], null, H(1));
            var wide = new VoteStateUpdate([new(9, 256)], null, H(1));

            // Act
            Action descendingAction = () => VoteProgram.CompactUpdateVoteState(Pk(1), Pk(2), descending);
            Action wideAction = () => VoteProgram.CompactUpdateVoteState(Pk(1), Pk(2), wide);

            // Assert
            descendingAction.Should().Throw<ArgumentException>();
            wideAction.Should().Throw<ArgumentException>();
        }
    }

    [TestFixture]
    public sealed class CompactUpdateVoteStateSwitch
    {
        [Test]
        public void MatchesShortVecAndUnsignedLeb128()
        {
            // Act
            var instruction = VoteProgram.CompactUpdateVoteStateSwitch(Pk(1), Pk(2), Update(), H(8));

            // Assert
            Hex(instruction).Should().Be("0d000000" + CompactBody() + Repeat(8, 32));
        }
    }

    [TestFixture]
    public sealed class TowerSync
    {
        [Test]
        public void MatchesShortVecAndUnsignedLeb128()
        {
            // Arrange
            var update = Update();
            var tower = new VoteTowerSync(update.Lockouts, update.Root, update.Hash, H(10), update.Timestamp);

            // Act
            var instruction = VoteProgram.TowerSync(Pk(1), Pk(2), tower);

            // Assert
            Hex(instruction).Should().Be("0e000000" + CompactBody() + Repeat(10, 32));
        }
    }

    [TestFixture]
    public sealed class TowerSyncSwitch
    {
        [Test]
        public void MatchesShortVecAndUnsignedLeb128()
        {
            // Arrange
            var update = Update();
            var tower = new VoteTowerSync(update.Lockouts, update.Root, update.Hash, H(10), update.Timestamp);

            // Act
            var instruction = VoteProgram.TowerSyncSwitch(Pk(1), Pk(2), tower, H(8));

            // Assert
            Hex(instruction).Should().Be("0f000000" + CompactBody() + Repeat(10, 32) + Repeat(8, 32));
        }
    }

    [TestFixture]
    public sealed class Vote
    {
        [Test]
        public void UsesPinnedDiscriminator()
            => Hex(VoteProgram.Vote(Pk(1), Pk(2), new([], H(3)))).Should().StartWith("02000000");
    }

    [TestFixture]
    public sealed class Withdraw
    {
        [Test]
        public void UsesPinnedDiscriminator()
            => Hex(VoteProgram.Withdraw(Pk(1), Pk(2), 9, Pk(3))).Should().Be("030000000900000000000000");
    }

    [TestFixture]
    public sealed class UpdateValidatorIdentity
    {
        [Test]
        public void UsesPinnedDiscriminator()
            => Hex(VoteProgram.UpdateValidatorIdentity(Pk(1), Pk(2), Pk(3))).Should().Be("04000000");
    }

    [TestFixture]
    public sealed class UpdateCommission
    {
        [Test]
        public void UsesPinnedDiscriminator()
            => Hex(VoteProgram.UpdateCommission(Pk(1), Pk(2), 7)).Should().Be("0500000007");
    }

    [TestFixture]
    public sealed class VoteSwitch
    {
        [Test]
        public void UsesPinnedDiscriminator()
            => Hex(VoteProgram.VoteSwitch(Pk(1), Pk(2), new([], H(3)), H(4)))
                .Should().StartWith("06000000");
    }

    [TestFixture]
    public sealed class AuthorizeChecked
    {
        [Test]
        public void UsesPinnedDiscriminator()
            => Hex(VoteProgram.AuthorizeChecked(Pk(1), Pk(2), Pk(3), VoteAuthorization.Voter))
                .Should().Be("0700000000000000");
    }

    [TestFixture]
    public sealed class UpdateVoteStateSwitch
    {
        [Test]
        public void UsesPinnedDiscriminator()
            => Hex(VoteProgram.UpdateVoteStateSwitch(
                    Pk(1),
                    Pk(2),
                    new([], null, H(3)),
                    H(4)))
                .Should().StartWith("09000000");
    }

    [TestFixture]
    public sealed class AuthorizeCheckedWithSeed
    {
        [Test]
        public void UsesPinnedDiscriminator()
            => Hex(VoteProgram.AuthorizeCheckedWithSeed(
                    Pk(1),
                    Pk(2),
                    Pk(4),
                    "ab",
                    Pk(3),
                    VoteAuthorization.Voter))
                .Should().StartWith("0b000000");
    }
}

public static class VoteInitializeTests
{
    [TestFixture]
    public sealed class Constructor
    {
        [Test]
        public void PreservesEveryLegacyInitializationField()
        {
            // Act
            var initialize = new VoteInitialize(Pk(1), Pk(2), Pk(3), 7);

            // Assert
            initialize.Node.Should().Be(Pk(1));
            initialize.AuthorizedVoter.Should().Be(Pk(2));
            initialize.AuthorizedWithdrawer.Should().Be(Pk(3));
            initialize.Commission.Should().Be(7);
            initialize.Should().Be(new VoteInitialize(Pk(1), Pk(2), Pk(3), 7));
        }
    }
}

internal static class VoteProgramParityTestHelpers
{
    internal static PublicKey Pk(byte value) => new([.. Enumerable.Repeat(value, PublicKey.Length)]);

    internal static Hash H(byte value) => new([.. Enumerable.Repeat(value, Hash.Length)]);

    internal static string Repeat(byte value, int count)
        => string.Concat(Enumerable.Repeat(value.ToString("x2"), count));

    internal static string Hex(Instruction instruction) => Convert.ToHexString(instruction.Data).ToLowerInvariant();

    internal static (PublicKey, bool, bool)[] Metas(Instruction instruction)
        => [.. instruction.Accounts.Select(static account => (account.PublicKey, account.IsSigner, account.IsWritable))];

    internal static VoteStateUpdate Update()
        => new([new(7, 1), new(300, 2)], 5, H(9), -2);

    internal static string CompactBody()
        => "0500000000000000" +
           "02" +
           "0201" +
           "a50202" +
           Repeat(9, 32) +
           "01feffffffffffffff";
}
