using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using SolSharp.Core.Converters;
using SolSharp.Core.Encoding;
using SolSharp.Core.Primitives;
using SolSharp.Programs;
using SolSharp.Rpc.Models.Parsed;
using SolSharp.Rpc.Protocol;
using SolSharp.Wallet;

namespace SolSharp.Benchmarks;

[MemoryDiagnoser]
public class SigningBenchmarks
{
    private readonly Keypair _keypair = Keypair.FromSeed(new byte[32]);
    private readonly byte[] _message = new byte[128];

    [Benchmark]
    public byte[] Sign() => _keypair.Sign(_message);

    [Benchmark]
    public bool Verify() => _keypair.PublicKey.Verify(_message, _keypair.Sign(_message));
}

[MemoryDiagnoser]
public class TransactionBenchmarks
{
    private const string Blockhash = "CktRuQ2mttgRGkXJtyksdKHjUdc2C4TgDzyB98oEzy8";
    private static readonly PublicKey Payer = new(Fill(1));
    private static readonly PublicKey Recipient = new(Fill(2));

    private readonly Keypair _signer = Keypair.FromSeed(Fill(1));
    private readonly Transaction _signed;
    private readonly byte[] _buffer;

    public TransactionBenchmarks()
    {
        _signed = BuildTransaction().Sign(_signer);
        _buffer = new byte[_signed.GetSerializedLength()];
    }

    [Benchmark]
    public Transaction CompileAndSign() => BuildTransaction().Sign(_signer);

    [Benchmark]
    public byte[] Serialize() => _signed.Serialize();

    [Benchmark]
    public bool TrySerializeIntoBuffer() => _signed.TrySerialize(_buffer, out _);

    private static Transaction BuildTransaction()
    {
        var message = new TransactionBuilder()
            .SetFeePayer(Payer)
            .SetRecentBlockhash(Blockhash)
            .AddInstructions(ComputeBudgetProgram.SetPriorityFee(200_000, 50_000))
            .AddInstruction(SystemProgram.Transfer(Payer, Recipient, 1_000_000))
            .BuildMessage();
        return Transaction.Create(message);
    }

    private static byte[] Fill(byte value)
    {
        var bytes = new byte[PublicKey.Length];
        Array.Fill(bytes, value);
        return bytes;
    }
}

[MemoryDiagnoser]
public class Base58Benchmarks
{
    private const string KeyBase58 = "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA";
    private static readonly byte[] KeyBytes = [.. Enumerable.Range(0, 32).Select(static i => (byte)i)];

    [Benchmark]
    public string Encode() => Base58.Encode(KeyBytes);

    [Benchmark]
    public byte[] Decode() => Base58.Decode(KeyBase58);

    [Benchmark]
    public PublicKey ParsePublicKey() => PublicKey.Parse(KeyBase58);
}

// Reflection variants are the 0.6.0 world (anonymous request params and models bound through the
// reflection resolver); source-gen variants are the 0.7.0 pipeline the clients actually use (RpcJson).
// Steady-state throughput is expected to be close - source generation's headline wins are first-call
// latency (no reflection-emit warmup, measured by the ColdStart pair on fresh options) and Native AOT.
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class JsonBenchmarks
{
    private const string ParsedTransactionJson =
        """{"transaction":{"signatures":["psig1"],"message":{"accountKeys":[{"pubkey":"3x9az88Dkbxa6tkKByxqEn7jBTJCJCD4dVvou49L24ET","signer":true,"writable":true,"source":"transaction"},{"pubkey":"11111111111111111111111111111111","signer":false,"writable":false,"source":"transaction"}],"instructions":[{"program":"system","programId":"11111111111111111111111111111111","parsed":{"type":"transfer","info":{"lamports":7}},"stackHeight":null}],"recentBlockhash":"Prbh1111111111111111111111111111111111111111"}},"meta":{"err":null,"fee":5000,"preBalances":[1,1],"postBalances":[1,1],"innerInstructions":[],"logMessages":[],"preTokenBalances":[],"postTokenBalances":[],"loadedAddresses":{"writable":[],"readonly":[]}},"slot":100,"blockTime":1700000000}""";

    private static readonly JsonSerializerOptions ReflectionOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly RpcRequest Request = RpcRequests.GetAccountInfo(
        PublicKey.Parse("TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA"), Commitment.Confirmed);

    [BenchmarkCategory("serialize request")]
    [Benchmark(Baseline = true)]
    public string SerializeRequest_Reflection()
        => JsonSerializer.Serialize(Request, ReflectionOptions);

    [BenchmarkCategory("serialize request")]
    [Benchmark]
    public string SerializeRequest_SourceGen()
        => JsonSerializer.Serialize(Request, RpcJson.TypeInfo<RpcRequest>());

    [BenchmarkCategory("deserialize parsed tx")]
    [Benchmark(Baseline = true)]
    public ParsedTransaction? DeserializeParsedTransaction_Reflection()
        => JsonSerializer.Deserialize<ParsedTransaction>(ParsedTransactionJson, ReflectionOptions);

    [BenchmarkCategory("deserialize parsed tx")]
    [Benchmark]
    public ParsedTransaction? DeserializeParsedTransaction_SourceGen()
        => JsonSerializer.Deserialize(ParsedTransactionJson, RpcJson.TypeInfo<ParsedTransaction>());

    // First serialization through freshly built options: metadata for the request graph is constructed
    // from scratch, which is where reflection pays its warmup cost. This approximates per-process
    // startup, not steady-state throughput.
    [BenchmarkCategory("cold start")]
    [Benchmark(Baseline = true)]
    public string ColdStart_Reflection()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };

        return JsonSerializer.Serialize(Request, options);
    }

    [BenchmarkCategory("cold start")]
    [Benchmark]
    public string ColdStart_SourceGen()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = JsonTypeInfoResolver.Combine(SolanaJsonContext.Default, CoreJsonContext.Default)
        };

        return JsonSerializer.Serialize(Request, options);
    }
}
