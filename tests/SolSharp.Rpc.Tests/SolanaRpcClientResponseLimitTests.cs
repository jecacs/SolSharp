using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace SolSharp.Rpc.Tests;

public static class SolanaRpcClientResponseLimitTests
{
    private const string SlotResponse = "{\"jsonrpc\":\"2.0\",\"result\":123,\"id\":1}";

    [TestFixture]
    public sealed class SingleRequest
    {
        [Test]
        public async Task DeclaredResponseBeyondConfiguredLimit_Throws()
        {
            var http = new HttpClient(new FakeHttpMessageHandler(SlotResponse))
            {
                BaseAddress = new Uri("http://localhost")
            };
            var client = new SolanaRpcClient(http, maximumResponseContentLength: 16);

            Func<Task> act = async () => await client.GetSlotAsync();

            await act.Should().ThrowAsync<HttpRequestException>()
                .WithMessage("*16-byte limit*");
        }

        [Test]
        public async Task UnknownLengthResponseAtConfiguredLimit_IsAccepted()
        {
            var content = UnknownLengthContent(SlotResponse);
            var handler = new SequenceHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
            var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            var client = new SolanaRpcClient(
                http, maximumResponseContentLength: Encoding.UTF8.GetByteCount(SlotResponse));
            content.Headers.ContentLength.Should().BeNull();

            (await client.GetSlotAsync()).Should().Be(123);
        }

        [Test]
        public async Task UnknownLengthResponseOneByteBeyondConfiguredLimit_ThrowsWhileReading()
        {
            var content = UnknownLengthContent(SlotResponse);
            var handler = new SequenceHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
            var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            var limit = Encoding.UTF8.GetByteCount(SlotResponse) - 1;
            var client = new SolanaRpcClient(http, maximumResponseContentLength: limit);
            content.Headers.ContentLength.Should().BeNull();

            Func<Task> act = async () => await client.GetSlotAsync();

            await act.Should().ThrowAsync<HttpRequestException>()
                .WithMessage($"*{limit}-byte limit*");
        }

        [Test]
        public async Task GenerousDefaultAcceptsLargeLegitimatePayload()
        {
            var paddedResponse = SlotResponse.PadRight(256 * 1024, ' ');
            var http = new HttpClient(new FakeHttpMessageHandler(paddedResponse))
            {
                BaseAddress = new Uri("http://localhost")
            };
            var client = new SolanaRpcClient(http);

            (await client.GetSlotAsync()).Should().Be(123);
        }
    }

    [TestFixture]
    public sealed class BatchRequest
    {
        [Test]
        public async Task ResponseBeyondConfiguredLimit_ThrowsAndFaultsQueuedCalls()
        {
            const string response = "[{\"jsonrpc\":\"2.0\",\"result\":123,\"id\":1}]";
            var http = new HttpClient(new FakeHttpMessageHandler(response))
            {
                BaseAddress = new Uri("http://localhost")
            };
            var client = new SolanaRpcClient(http, maximumResponseContentLength: 16);
            var batch = client.CreateBatch();
            var call = batch.GetSlotAsync();

            var act = () => batch.ExecuteAsync();

            await act.Should().ThrowAsync<HttpRequestException>()
                .WithMessage("*16-byte limit*");
            call.IsFaulted.Should().BeTrue();
        }
    }

    [TestFixture]
    public sealed class DependencyInjection
    {
        [Test]
        public async Task ConfiguredLimitFlowsThroughTypedClientActivation()
        {
            var services = new ServiceCollection();
            services
                .AddSolanaRpc(options =>
                {
                    options.Endpoint = "https://node.example";
                    options.MaximumResponseContentLength = 16;
                })
                .ConfigurePrimaryHttpMessageHandler(() => new FakeHttpMessageHandler(SlotResponse));
            using var provider = services.BuildServiceProvider();
            var client = provider.GetRequiredService<SolanaRpcClient>();

            Func<Task> act = async () => await client.GetSlotAsync();

            await act.Should().ThrowAsync<HttpRequestException>()
                .WithMessage("*16-byte limit*");
        }
    }

    private static StreamContent UnknownLengthContent(string body)
    {
        var content = new StreamContent(new NonSeekableReadStream(Encoding.UTF8.GetBytes(body)));
        content.Headers.ContentType = new("application/json");
        return content;
    }

    private sealed class NonSeekableReadStream(byte[] data) : Stream
    {
        private readonly MemoryStream _inner = new(data, writable: false);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
            => _inner.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
            => _inner.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _inner.Dispose();
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _inner.DisposeAsync();
            await base.DisposeAsync();
        }
    }
}
