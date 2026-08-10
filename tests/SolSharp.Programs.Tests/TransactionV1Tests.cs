using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;
using SolSharp.Wallet;
using FsCheckProperty = FsCheck.NUnit.PropertyAttribute;

namespace SolSharp.Programs.Tests;

public static class TransactionV1Tests
{
    private static PublicKey Pk(byte value) => new(Fill(value));

    private static byte[] Fill(byte value) => [.. Enumerable.Repeat(value, PublicKey.Length)];

    // solana-sdk ec7a0467e268774b724d55120ad952b518f27d64
    // message/src/versions/v1/message.rs::byte_layout_without_config, version-prefixed for a transaction.
    private static byte[] UpstreamMessage()
    {
        var bytes = new List<byte> { MessageV1.VersionPrefix, 1, 0, 0 };
        bytes.AddRange(new byte[sizeof(uint)]);
        bytes.AddRange(Fill(0xAB));
        bytes.Add(1);
        bytes.Add(2);
        bytes.AddRange(Fill(1));
        bytes.AddRange(Fill(2));
        bytes.Add(1);
        bytes.Add(1);
        bytes.AddRange([2, 0]);
        bytes.Add(0);
        bytes.AddRange([0xDE, 0xAD]);
        return [.. bytes];
    }

    private static MessageV1 SimpleMessage(int dataLength = 1)
    {
        var instruction = new Instruction
        {
            ProgramId = Pk(2),
            Accounts = [AccountMeta.WritableSigner(Pk(1))],
            Data = new byte[dataLength]
        };
        return MessageV1.Compile(Pk(1), new Hash(Fill(0xAB)), [instruction]);
    }

    [TestFixture]
    public sealed class Create
    {
        [Test]
        public void UnsignedTransaction_IsMessageThenFixedSignatures_MatchingPinnedUpstream()
        {
            // Arrange
            var messageBytes = UpstreamMessage();
            var message = MessageV1.Deserialize(messageBytes);

            // Act
            var transaction = Transaction.Create(message);
            var bytes = transaction.Serialize();

            // Assert
            transaction.Version.Should().Be(TransactionVersion.V1);
            transaction.GetSerializedLength().Should().Be(messageBytes.Length + Transaction.SignatureLength);
            bytes[..messageBytes.Length].Should().Equal(messageBytes);
            bytes[messageBytes.Length..].Should().OnlyContain(static value => value == 0);
            bytes[0].Should().Be(MessageV1.VersionPrefix);
        }
    }

    [TestFixture]
    public sealed class Sign
    {
        [Test]
        public void SigningPlacesEd25519SignatureAfterExactMessageBytes()
        {
            // Arrange
            using var payer = Keypair.FromSeed(Fill(7));
            var instruction = new Instruction
            {
                ProgramId = Pk(9),
                Accounts = [AccountMeta.WritableSigner(payer.PublicKey)],
                Data = [1, 2, 3]
            };
            var message = MessageV1.Compile(
                payer.PublicKey,
                new Hash(Fill(8)),
                [instruction],
                new()
                {
                    ComputeUnitLimit = 200_000,
                    LoadedAccountsDataSizeLimit = 1_000_000
                });
            var signableBytes = message.Serialize();

            // Act
            var bytes = Transaction.Create(message).Sign(payer).Serialize();

            // Assert
            bytes[..signableBytes.Length].Should().Equal(signableBytes);
            bytes.Length.Should().Be(signableBytes.Length + Signature.Length);
            payer.PublicKey.Verify(signableBytes, bytes.AsSpan(signableBytes.Length)).Should().BeTrue();
        }
    }

    [TestFixture]
    public sealed class TrySerialize
    {
        [Test]
        public void TrySerialize_MatchesAllocatingPathAndExactLength()
        {
            // Arrange
            var transaction = Transaction.Create(SimpleMessage());
            var expected = transaction.Serialize();
            var destination = new byte[transaction.GetSerializedLength()];

            // Act
            var success = transaction.TrySerialize(destination, out var written);

            // Assert
            success.Should().BeTrue();
            written.Should().Be(expected.Length);
            destination.Should().Equal(expected);
        }

        [Test]
        public void TrySerialize_ShortSpanReturnsFalse()
        {
            // Arrange
            var transaction = Transaction.Create(SimpleMessage());

            // Act
            var success = transaction.TrySerialize(new byte[transaction.GetSerializedLength() - 1], out var written);

            // Assert
            success.Should().BeFalse();
            written.Should().Be(0);
        }
    }

    [TestFixture]
    public sealed class Serialize
    {
        [Test]
        public void DeserializedMessageMutation_DoesNotChangeCapturedWireBytes()
        {
            // Arrange
            byte[] bytes = [.. UpstreamMessage(), .. new byte[Transaction.SignatureLength]];
            var transaction = Transaction.Deserialize(bytes);

            // Act
            transaction.Message.Instructions[0].Data[0] ^= 0xFF;

            // Assert
            transaction.Serialize().Should().Equal(bytes);
        }

        [TestCase(3_921, MessageV1.MaxTransactionSize)]
        [TestCase(3_922, MessageV1.MaxTransactionSize + 1)]
        public void CodecRoundTripsAtAndAboveRpcRuntimeSizeBoundary(int dataLength, int expectedSize)
        {
            // Arrange: pinned upstream wincode intentionally round-trips both 4096 and 4097 bytes;
            // packet/RPC admission, not this codec, applies MAX_TRANSACTION_SIZE.
            var transaction = Transaction.Create(SimpleMessage(dataLength));

            // Act
            var bytes = transaction.Serialize();
            var parsed = Transaction.Deserialize(bytes);

            // Assert
            bytes.Should().HaveCount(expectedSize);
            parsed.Serialize().Should().Equal(bytes);
        }
    }

    [TestFixture]
    public sealed class Deserialize
    {
        [FsCheckProperty(
            MaxTest = 1_000,
            Replay = "3405691582,3131961357",
            QuietOnSuccess = true)]
        public bool SingleByteMutation_EitherRejectsOrRoundTrips(int index, byte replacement)
        {
            // Arrange
            byte[] data = [.. UpstreamMessage(), .. new byte[Transaction.SignatureLength]];
            var position = (int)((uint)index % (uint)data.Length);
            data[position] = replacement;

            // Act & Assert
            try
            {
                var transaction = Transaction.Deserialize(data);
                return transaction.Serialize().AsSpan().SequenceEqual(data);
            }
            catch (FormatException)
            {
                return true;
            }
        }

        [FsCheckProperty(
            MaxTest = 500,
            Replay = "232525822,19088743",
            QuietOnSuccess = true)]
        public bool ProperPrefix_IsAlwaysRejected(int lengthSelector)
        {
            // Arrange
            byte[] data = [.. UpstreamMessage(), .. new byte[Transaction.SignatureLength]];
            var length = (int)((uint)lengthSelector % (uint)data.Length);

            // Act & Assert
            try
            {
                _ = Transaction.Deserialize(data.AsSpan(0, length));
                return false;
            }
            catch (FormatException)
            {
                return true;
            }
        }

        [Test]
        public void UnsignedTransaction_RoundTripsWithoutSignatureCountPrefix()
        {
            // Arrange
            byte[] bytes = [.. UpstreamMessage(), .. new byte[Transaction.SignatureLength]];

            // Act
            var transaction = Transaction.Deserialize(bytes);

            // Assert
            transaction.Version.Should().Be(TransactionVersion.V1);
            transaction.Message.Should().BeOfType<MessageV1>();
            transaction.Serialize().Should().Equal(bytes);
        }

        [TestCase(0x80)]
        [TestCase(0x82)]
        [TestCase(0xFF)]
        public void UnknownHighBitTransactionDiscriminator_Throws(byte discriminator)
        {
            // Arrange
            byte[] bytes = [discriminator];

            // Act
            Action act = () => Transaction.Deserialize(bytes);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*transaction discriminator*");
        }

        [Test]
        public void V1MessageInsideLegacyEnvelope_Throws()
        {
            // Arrange: zero legacy signatures followed by a forbidden V1 message.
            byte[] bytes = [0, .. UpstreamMessage(), .. new byte[Transaction.SignatureLength]];

            // Act
            Action act = () => Transaction.Deserialize(bytes);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*Invalid message version byte 0x81*");
        }

        [Test]
        public void MissingFixedSignature_Throws()
        {
            // Arrange
            var bytes = UpstreamMessage();

            // Act
            Action act = () => Transaction.Deserialize(bytes);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*requires 1 signature slot(s)*0 byte(s) remain*");
        }

        [Test]
        public void ExtraFixedSignatureByte_Throws()
        {
            // Arrange
            byte[] bytes = [.. UpstreamMessage(), .. new byte[Transaction.SignatureLength + 1]];

            // Act
            Action act = () => Transaction.Deserialize(bytes);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*64 bytes*65 byte(s) remain*");
        }

        [Test]
        public void TruncatedV1Message_ThrowsDocumentedFormatException()
        {
            // Arrange
            byte[] bytes = [MessageV1.VersionPrefix, 1, 0];

            // Act
            Action act = () => Transaction.Deserialize(bytes);

            // Assert
            act.Should().Throw<FormatException>();
        }
    }

    [TestFixture]
    public sealed class BuildMessageV1
    {
        [Test]
        public void BuildMessageV1_AppliesTypedLifetimeAndAllInlineConfig()
        {
            // Arrange
            var hash = new Hash(Fill(8));
            var config = new TransactionConfigV1
            {
                PriorityFee = 500,
                ComputeUnitLimit = 200_000,
                LoadedAccountsDataSizeLimit = 1_000_000,
                HeapSize = 65_536
            };
            var instruction = new Instruction
            {
                ProgramId = Pk(9),
                Accounts = [AccountMeta.WritableSigner(Pk(1)), AccountMeta.Readonly(Pk(2))],
                Data = [7]
            };

            // Act
            var message = new TransactionBuilder()
                .SetFeePayer(Pk(1))
                .SetRecentBlockhash(hash)
                .SetV1Config(config)
                .AddInstruction(instruction)
                .BuildMessageV1();

            // Assert
            message.LifetimeSpecifier.Should().Be(hash);
            message.Config.Should().Be(config);
            message.DecompileInstructions().Should().ContainSingle();
            message.Serialize()[0].Should().Be(MessageV1.VersionPrefix);
        }

        [Test]
        public void AddressLookupTables_AreRejectedForV1()
        {
            // Arrange
            var builder = new TransactionBuilder()
                .SetFeePayer(Pk(1))
                .SetRecentBlockhash(new Hash(Fill(8)))
                .SetAddressLookupTables(new AddressLookupTableAccount(Pk(5), [Pk(2)]))
                .AddInstruction(new() { ProgramId = Pk(9), Accounts = [], Data = [] });

            // Act
            Action act = () => builder.BuildMessageV1();

            // Assert
            act.Should().Throw<InvalidOperationException>().WithMessage("*do not support address lookup tables*");
        }
    }

    [TestFixture]
    public sealed class BuildV1
    {
        [Test]
        public void BuildV1_InfersFeePayerAndSignsMessageFirstWire()
        {
            // Arrange
            using var payer = Keypair.FromSeed(Fill(7));
            var instruction = new Instruction
            {
                ProgramId = Pk(9),
                Accounts = [AccountMeta.WritableSigner(payer.PublicKey)],
                Data = [7]
            };

            // Act
            var transaction = new TransactionBuilder()
                .SetRecentBlockhash(new Hash(Fill(8)))
                .SetV1Config(new()
                {
                    ComputeUnitLimit = 200_000,
                    LoadedAccountsDataSizeLimit = 1_000_000
                })
                .AddInstruction(instruction)
                .BuildV1(payer);
            var bytes = transaction.Serialize();
            var messageBytes = transaction.Message.Serialize();

            // Assert
            transaction.Version.Should().Be(TransactionVersion.V1);
            transaction.Message.AccountKeys[0].Should().Be(payer.PublicKey);
            bytes[..messageBytes.Length].Should().Equal(messageBytes);
            payer.PublicKey.Verify(messageBytes, bytes.AsSpan(messageBytes.Length)).Should().BeTrue();
        }
    }

    [TestFixture]
    public sealed class SetV1Config
    {
        [Test]
        public void NullConfig_ThrowsAtSetter()
        {
            // Arrange
            var builder = new TransactionBuilder();

            // Act
            Action act = () => builder.SetV1Config(null!);

            // Assert
            act.Should().Throw<ArgumentNullException>().WithParameterName("config");
        }
    }
}
