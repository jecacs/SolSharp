using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Converters;

namespace SolSharp.Core.Tests.Converters;

public static class FixedWidthJsonConverterTests
{
    [TestFixture]
    public sealed class OversizedSegmentedToken
    {
        [Test]
        public void PublicKey_IsRejectedBeforeMaterializingTheSequence()
        {
            var result = ReadOversized(new PublicKeyJsonConverter());
            result.Exception.Message.Should().Be("Invalid public key base58 value.");
            result.Allocated.Should().BeLessThan(256_000);
        }

        [Test]
        public void Hash_IsRejectedBeforeMaterializingTheSequence()
        {
            var result = ReadOversized(new HashJsonConverter());
            result.Exception.Message.Should().Be("Invalid hash base58 value.");
            result.Allocated.Should().BeLessThan(256_000);
        }

        [Test]
        public void Commitment_IsRejectedWithoutMaterializingTheSequence()
        {
            var result = ReadOversized(new CommitmentJsonConverter());
            result.Exception.Message.Should().Be("Unknown commitment value.");
            result.Allocated.Should().BeLessThan(256_000);
        }

        private static (JsonException Exception, long Allocated) ReadOversized<T>(JsonConverter<T> converter)
        {
            var first = new BufferSegment(System.Text.Encoding.UTF8.GetBytes('"' + new string('z', 500_000)));
            var last = first.Append(System.Text.Encoding.UTF8.GetBytes(new string('z', 500_000) + '"'));
            var sequence = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
            var reader = new Utf8JsonReader(sequence);
            reader.Read().Should().BeTrue();
            reader.HasValueSequence.Should().BeTrue();
            var options = JsonSerializerOptions.Default;
            var before = GC.GetAllocatedBytesForCurrentThread();

            try
            {
                _ = converter.Read(ref reader, typeof(T), options);
                throw new AssertionException("The converter accepted an oversized segmented JSON token.");
            }
            catch (JsonException exception)
            {
                return (exception, GC.GetAllocatedBytesForCurrentThread() - before);
            }
        }

        private sealed class BufferSegment : ReadOnlySequenceSegment<byte>
        {
            public BufferSegment(ReadOnlyMemory<byte> memory) => Memory = memory;

            public BufferSegment Append(ReadOnlyMemory<byte> memory)
            {
                var segment = new BufferSegment(memory)
                {
                    RunningIndex = RunningIndex + Memory.Length
                };
                Next = segment;
                return segment;
            }
        }
    }
}
