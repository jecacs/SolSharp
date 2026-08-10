using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs.Tests;

public static class MessageDecompileTests
{
    private static PublicKey Pk(byte value) => new([.. Enumerable.Repeat(value, PublicKey.Length)]);

    private static (PublicKey, bool, bool)[] Metas(Instruction instruction)
        => [.. instruction.Accounts.Select(static a => (a.PublicKey, a.IsSigner, a.IsWritable))];

    [TestFixture]
    public sealed class MessageDecompileInstructions
    {
        [Test]
        public void ReproducesAllFourAccountClasses()
        {
            // Arrange
            var instruction = new Instruction
            {
                ProgramId = Pk(9),
                Accounts =
                [
                    AccountMeta.WritableSigner(Pk(1)),
                    AccountMeta.ReadonlySigner(Pk(2)),
                    AccountMeta.Writable(Pk(3)),
                    AccountMeta.Readonly(Pk(4))
                ],
                Data = [7]
            };
            var message = Message.Compile(Pk(1), Pk(8).ToString(), [instruction]);

            // Act
            var decompiled = message.DecompileInstructions([]).Should().ContainSingle().Subject;

            // Assert
            decompiled.ProgramId.Should().Be(Pk(9));
            decompiled.Data.Should().Equal(7);
            Metas(decompiled).Should().Equal(
                (Pk(1), true, true),
                (Pk(2), true, false),
                (Pk(3), false, true),
                (Pk(4), false, false));
        }

        [Test]
        public void MutatingDecompiledData_DoesNotMutateMessage()
        {
            // Arrange
            var instruction = new Instruction { ProgramId = Pk(9), Accounts = [], Data = [7] };
            var message = Message.Compile(Pk(1), Pk(8).ToString(), [instruction]);
            var decompiled = message.DecompileInstructions([]).Should().ContainSingle().Subject;

            // Act
            decompiled.Data[0] = 99;

            // Assert
            message.Instructions[0].Data.Should().Equal(7);
        }

        [Test]
        public void PreservesMultipleInstructionOrderAndRepeatedAccounts()
        {
            // Arrange
            var repeated = Pk(3);
            var first = new Instruction
            {
                ProgramId = Pk(9),
                Accounts = [AccountMeta.Writable(repeated), AccountMeta.Writable(repeated)],
                Data = [1]
            };
            var second = new Instruction
            {
                ProgramId = Pk(10),
                Accounts = [AccountMeta.Readonly(Pk(4))],
                Data = [2, 3]
            };
            var message = Message.Compile(Pk(1), Pk(8).ToString(), [first, second]);

            // Act
            var decompiled = message.DecompileInstructions([]);

            // Assert
            decompiled.Select(static instruction => instruction.ProgramId).Should().Equal(Pk(9), Pk(10));
            decompiled.Select(static instruction => instruction.Data).Should().SatisfyRespectively(
                static data => data.Should().Equal(1),
                static data => data.Should().Equal(2, 3));
            Metas(decompiled[0]).Should().Equal(
                (repeated, false, true),
                (repeated, false, true));
            Metas(decompiled[1]).Should().Equal((Pk(4), false, false));
        }

        [Test]
        public void NullLookupTableList_ThrowsDocumentedException()
        {
            // Arrange
            var message = Message.Compile(Pk(1), Pk(8).ToString(), []);

            // Act
            Action act = () => _ = message.DecompileInstructions(null!);

            // Assert
            act.Should().Throw<ArgumentNullException>().WithParameterName("lookupTables");
        }
    }

    [TestFixture]
    public sealed class MessageV0DecompileInstructions
    {
        [Test]
        public void ResolvesLookupTableAccounts()
        {
            // Arrange
            var instruction = new Instruction
            {
                ProgramId = Pk(9),
                Accounts =
                [
                    AccountMeta.Writable(Pk(2)),
                    AccountMeta.Readonly(Pk(3)),
                    AccountMeta.Writable(Pk(4)),
                    AccountMeta.WritableSigner(Pk(6))
                ],
                Data = [1, 2]
            };
            var table = new AddressLookupTableAccount(Pk(5), [Pk(2), Pk(3), Pk(7)]);
            var message = MessageV0.Compile(Pk(1), Pk(8).ToString(), [instruction], [table]);

            // Act
            var decompiled = message.DecompileInstructions([table]).Should().ContainSingle().Subject;

            // Assert
            decompiled.ProgramId.Should().Be(Pk(9));
            decompiled.Data.Should().Equal(1, 2);
            Metas(decompiled).Should().Equal(
                (Pk(2), false, true),
                (Pk(3), false, false),
                (Pk(4), false, true),
                (Pk(6), true, true));
        }

        [Test]
        public void WithoutTheTable_Throws()
        {
            // Arrange
            var instruction = new Instruction
            {
                ProgramId = Pk(9),
                Accounts = [AccountMeta.Writable(Pk(2))],
                Data = [1]
            };
            var table = new AddressLookupTableAccount(Pk(5), [Pk(2)]);
            var message = MessageV0.Compile(Pk(1), Pk(8).ToString(), [instruction], [table]);

            // Act
            Action act = () => _ = message.DecompileInstructions([]);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void ResolvesMultipleTablesAndPreservesOriginalAccountOrder()
        {
            // Arrange
            var instruction = new Instruction
            {
                ProgramId = Pk(9),
                Accounts =
                [
                    AccountMeta.Readonly(Pk(13)),
                    AccountMeta.Writable(Pk(4)),
                    AccountMeta.Writable(Pk(12)),
                    AccountMeta.Readonly(Pk(3)),
                    AccountMeta.ReadonlySigner(Pk(6)),
                    AccountMeta.Writable(Pk(2)),
                    AccountMeta.WritableSigner(Pk(1)),
                    AccountMeta.Readonly(Pk(13))
                ],
                Data = [4, 5]
            };
            var firstTable = new AddressLookupTableAccount(Pk(5), [Pk(2), Pk(3)]);
            var secondTable = new AddressLookupTableAccount(Pk(15), [Pk(12), Pk(13)]);
            var message = MessageV0.Compile(
                Pk(1), Pk(8).ToString(), [instruction], [firstTable, secondTable]);

            // Act
            var decompiled = message.DecompileInstructions([firstTable, secondTable])
                .Should().ContainSingle().Subject;

            // Assert
            message.AddressTableLookups.Should().HaveCount(2);
            Metas(decompiled).Should().Equal(
                (Pk(13), false, false),
                (Pk(4), false, true),
                (Pk(12), false, true),
                (Pk(3), false, false),
                (Pk(6), true, false),
                (Pk(2), false, true),
                (Pk(1), true, true),
                (Pk(13), false, false));
        }

        [Test]
        public void SuppliedTableWithMissingAddress_Throws()
        {
            // Arrange
            var instruction = new Instruction
            {
                ProgramId = Pk(9),
                Accounts = [AccountMeta.Writable(Pk(2))],
                Data = []
            };
            var completeTable = new AddressLookupTableAccount(Pk(5), [Pk(2)]);
            var message = MessageV0.Compile(Pk(1), Pk(8).ToString(), [instruction], [completeTable]);
            var truncatedTable = new AddressLookupTableAccount(Pk(5), []);

            // Act
            Action act = () => _ = message.DecompileInstructions([truncatedTable]);

            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("Lookup index 0 is out of range*");
        }

        [Test]
        public void NullLookupTableList_ThrowsDocumentedException()
        {
            // Arrange
            var message = MessageV0.Compile(Pk(1), Pk(8).ToString(), [], []);

            // Act
            Action act = () => _ = message.DecompileInstructions(null!);

            // Assert
            act.Should().Throw<ArgumentNullException>().WithParameterName("lookupTables");
        }
    }

    [TestFixture]
    public sealed class MessageV0GetAccountKeys
    {
        [Test]
        public void LookupTables_ProduceTheCompleteStaticAndLoadedIndexSpace()
        {
            // Arrange
            var instruction = new Instruction
            {
                ProgramId = Pk(9),
                Accounts =
                [
                    AccountMeta.Writable(Pk(2)),
                    AccountMeta.Readonly(Pk(3)),
                    AccountMeta.Writable(Pk(4)),
                    AccountMeta.WritableSigner(Pk(6))
                ],
                Data = [1, 2]
            };
            var table = new AddressLookupTableAccount(Pk(5), [Pk(2), Pk(3), Pk(7)]);
            var message = MessageV0.Compile(Pk(1), Pk(8).ToString(), [instruction], [table]);

            // Act
            var keys = message.GetAccountKeys([table]);

            // Assert
            keys.Should().Equal(Pk(1), Pk(6), Pk(4), Pk(9), Pk(2), Pk(3));
        }

        [Test]
        public void NullLookupTableList_ThrowsDocumentedException()
        {
            // Arrange
            var message = MessageV0.Compile(Pk(1), Pk(8).ToString(), [], []);

            // Act
            Action act = () => _ = message.GetAccountKeys(null!);

            // Assert
            act.Should().Throw<ArgumentNullException>().WithParameterName("lookupTables");
        }
    }

    [TestFixture]
    public sealed class MessageV1DecompileInstructions
    {
        [Test]
        public void ParameterlessAndLookupTableOverloads_ResolveTheSameInlineAccounts()
        {
            // Arrange
            var instruction = new Instruction
            {
                ProgramId = Pk(9),
                Accounts =
                [
                    AccountMeta.WritableSigner(Pk(1)),
                    AccountMeta.Readonly(Pk(2)),
                    AccountMeta.Writable(Pk(3))
                ],
                Data = [7]
            };
            var message = MessageV1.Compile(Pk(1), new Hash(Pk(8).ToBytes()), [instruction]);

            // Act
            var direct = message.DecompileInstructions().Should().ContainSingle().Subject;
            var withLookupTables = message.DecompileInstructions([]).Should().ContainSingle().Subject;

            // Assert
            direct.ProgramId.Should().Be(Pk(9));
            direct.Data.Should().Equal(7);
            Metas(direct).Should().Equal(
                (Pk(1), true, true),
                (Pk(2), false, false),
                (Pk(3), false, true));
            Metas(withLookupTables).Should().Equal(Metas(direct));
        }

        [Test]
        public void NullLookupTableList_ThrowsDocumentedException()
        {
            // Arrange
            var message = MessageV1.Compile(Pk(1), new Hash(Pk(8).ToBytes()), []);

            // Act
            Action act = () => _ = message.DecompileInstructions(null!);

            // Assert
            act.Should().Throw<ArgumentNullException>().WithParameterName("lookupTables");
        }
    }

    [TestFixture]
    public sealed class ITransactionMessageDecompileInstructions
    {
        [Test]
        public void LegacyMessage_DefaultMethodUsesAnEmptyLookupTableList()
        {
            // Arrange
            var instruction = new Instruction { ProgramId = Pk(9), Accounts = [], Data = [7] };
            var message = Message.Compile(Pk(1), Pk(8).ToString(), [instruction]);

            // Act & Assert
            ((ITransactionMessage)message).DecompileInstructions().Should().ContainSingle();
        }

        [Test]
        public void VersionZeroMessageWithLookups_DefaultMethodRejectsTheMissingTable()
        {
            // Arrange
            var instruction = new Instruction
            {
                ProgramId = Pk(9),
                Accounts = [AccountMeta.Writable(Pk(2))],
                Data = []
            };
            var table = new AddressLookupTableAccount(Pk(5), [Pk(2)]);
            var message = MessageV0.Compile(Pk(1), Pk(8).ToString(), [instruction], [table]);

            // Act
            Action act = () => _ = ((ITransactionMessage)message).DecompileInstructions();

            // Assert
            act.Should().Throw<ArgumentException>();
        }
    }
}
