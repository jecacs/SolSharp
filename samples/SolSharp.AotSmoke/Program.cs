// The Native AOT smoke check: published with PublishAot and run as a native binary in CI, it exercises
// the paths that would break if reflection crept back in - the source-generated JSON pipeline (request
// serialization, response envelopes, the hand-written converters), Ed25519 signing, transaction
// compilation and serialization, and PDA/ATA derivation. Everything runs offline against a canned
// HTTP handler; any failure throws and fails the job through the non-zero exit code.
using System.Net;
using System.Security.Cryptography;
using System.Text;
using SolSharp.Core.Primitives;
using SolSharp.Programs;
using SolSharp.Rpc;
using SolSharp.Wallet;

const string blockhash = "CktRuQ2mttgRGkXJtyksdKHjUdc2C4TgDzyB98oEzy8";
const string systemProgram = "11111111111111111111111111111111";
const string signatureBase58 = "5VERv8NMvzbJMEkV8xnrLkEaWRtSz9CosKDYjCJjBRnbJLgp8uirBgmQpjKhoR4tjF3ZpRzrFmBV6UjKdiSZkQUW";

using var keypair = Keypair.FromSeed(new byte[32]);
var message = "solsharp aot smoke"u8.ToArray();
var signature = keypair.SignSignature(message);
Check(signature.Verify(keypair.PublicKey, message), "typed sign/verify round-trip");

var keyJson = keypair.ToJsonArray();
using var importedKeypair = Keypair.FromJsonArray(keyJson);
Check(importedKeypair.PublicKey == keypair.PublicKey, "AOT key-file export/import");

var offchain = OffchainMessage.Create("solsharp aot off-chain smoke");
var offchainSignature = offchain.Sign(keypair);
var parsedOffchain = OffchainMessage.Deserialize(offchain.Serialize());
Check(parsedOffchain.Verify(keypair.PublicKey, offchainSignature), "off-chain message round-trip");

var exportedSecret = keypair.ToBytes();
try
{
    using var importedSecret = Keypair.FromSecretKey(exportedSecret);
    Check(importedSecret.PublicKey == keypair.PublicKey, "64-byte secret export/import");
}
finally
{
    CryptographicOperations.ZeroMemory(exportedSecret);
}

var recipient = PublicKey.Parse(systemProgram);
using var blsKeypair = BlsKeypair.Derive(Enumerable.Range(0, 32).Select(static value => (byte)value).ToArray());
var blsSignature = blsKeypair.Sign(message);
var blsProof = blsKeypair.CreateVoteProofOfPossession(recipient);
Check(blsKeypair.Verify(blsSignature, message), "BLS sign/verify through PoP-verified keypair boundary");
Check(
    blsKeypair.PublicKey.VerifyVoteProofOfPossession(blsProof, recipient),
    "BLS vote proof-of-possession binding");
Check(BlsPublicKey.Parse(blsKeypair.PublicKey.ToString()).Equals(blsKeypair.PublicKey), "BLS base64 round-trip");
using var secondBlsKeypair = BlsKeypair.Derive(Enumerable.Range(1, 32).Select(static value => (byte)value).ToArray());
var firstVerifiedBlsKey = blsKeypair.PublicKey.VerifyAndWrapProofOfPossession(
    blsKeypair.CreateProofOfPossession("aot-aggregate"u8),
    "aot-aggregate"u8);
var secondVerifiedBlsKey = secondBlsKeypair.PublicKey.VerifyAndWrapProofOfPossession(
    secondBlsKeypair.CreateProofOfPossession("aot-aggregate"u8),
    "aot-aggregate"u8);
var aggregateBlsKey = BlsAggregatePublicKey.Aggregate([firstVerifiedBlsKey, secondVerifiedBlsKey]);
var aggregateBlsSignature = BlsSignature.Aggregate([blsSignature, secondBlsKeypair.Sign(message)]);
Check(aggregateBlsKey.Verify(aggregateBlsSignature, message), "BLS same-message aggregate verification");
var blsKeypairBytes = blsKeypair.ToBytes();
byte[]? blsKeypairJson = null;
try
{
    blsKeypairJson = blsKeypair.ToJsonUtf8Bytes();
    using var importedBls = BlsKeypair.FromBytes(blsKeypairBytes);
    using var jsonBls = BlsKeypair.FromJsonArray(blsKeypairJson);
    Check(importedBls.PublicKey.Equals(jsonBls.PublicKey), "BLS keypair binary/JSON round-trip");
}
finally
{
    CryptographicOperations.ZeroMemory(blsKeypairBytes);
    if (blsKeypairJson is not null)
        CryptographicOperations.ZeroMemory(blsKeypairJson);
}

var transferInstruction = SystemProgram.Transfer(keypair.PublicKey, recipient, 1_000_000);
var transactionMessage = new TransactionBuilder()
    .SetFeePayer(keypair.PublicKey)
    .SetRecentBlockhash(blockhash)
    .AddInstruction(transferInstruction)
    .BuildMessage();

var transaction = Transaction.Create(transactionMessage).Sign(keypair);
var wire = transaction.Serialize();
Check(wire.Length == transaction.GetSerializedLength(), "transaction serialization length");
Check(transaction.VerifyAndHashMessage() == transaction.GetMessageHash(), "transaction verify and message hash");

var v1Message = new TransactionBuilder()
    .SetFeePayer(keypair.PublicKey)
    .SetRecentBlockhash(blockhash)
    .SetV1Config(new()
    {
        ComputeUnitLimit = 200_000,
        LoadedAccountsDataSizeLimit = 64 * 1024
    })
    .AddInstruction(SystemProgram.Transfer(keypair.PublicKey, recipient, 1))
    .BuildMessageV1();
var v1Transaction = Transaction.Create(v1Message).SignAll(keypair);
var v1Wire = v1Transaction.Serialize();
var parsedV1 = Transaction.Deserialize(v1Wire);
Check(parsedV1.Version == TransactionVersion.V1 && parsedV1.VerifySignatures(), "V1 transaction round-trip");

var (pda, _) = ProgramDerivedAddress.FindProgramAddress(["smoke"u8.ToArray()], recipient);
var ata = AssociatedTokenAccount.GetAddress(keypair.PublicKey, recipient);
Check(pda != default && ata != default, "PDA/ATA derivation");

var instructionSysvar = InstructionsSysvar.Serialize([transferInstruction]);
var introspected = InstructionsSysvar.ReadInstruction(instructionSysvar, 0);
Check(introspected.ProgramId == SystemProgram.ProgramId, "Instructions sysvar round-trip");

using var http = new HttpClient(new CannedRpcHandler());
http.BaseAddress = new("http://localhost");
var client = new SolanaRpcClient(http);

var latest = await client.GetLatestBlockhashAsync();
Check(latest.Blockhash == blockhash, "getLatestBlockhash");

var account = await client.GetAccountInfoAsync(recipient);
Check(account is { Lamports: 42, Data: [1, 2, 3] }, "getAccountInfo");

var programAccounts = await client.GetProgramAccountsAsync(
    recipient,
    new()
    {
        Filters =
        [
            AccountFilter.MemoryCompareRaw(ulong.MaxValue, [0, 1, 2]),
            AccountFilter.TokenAccountState()
        ]
    });
Check(programAccounts.Count == 0, "getProgramAccounts full filter union");

var simulation = await client.SimulateTransactionAsync(
    wire,
    new() { Accounts = [recipient], InnerInstructions = true });
Check(
    simulation is
    {
        Fee: 5_000,
        LoadedAccountsDataSize: 3,
        Accounts: [{ Space: 3 }],
        ReturnData.Data: [4, 5]
    },
    "simulateTransaction current fields");

var agGenesis = await client.GetAgGenesisCertificateAsync();
Check(agGenesis is null, "getAgGenesisCert nullable result");

var rawV1 = await client.GetTransactionWithMaxVersionAsync(signatureBase58, 1);
Check(
    rawV1 is { Transaction: [0x81, 1, 2, 3] } && rawV1.Version?.Number == 1,
    "raw transaction V1 opt-in");

var parsedRpcV1 = await client.GetParsedTransactionWithMaxVersionAsync(signatureBase58, 1);
Check(
    parsedRpcV1?.Message.TransactionConfig is
    {
        PriorityFee: 5_000,
        ComputeUnitLimit: 200_000,
        LoadedAccountsDataSizeLimit: 65_536,
        HeapSize: 32_768
    },
    "parsed transaction V1 config");

var sent = await client.SendTransactionAsync(wire);
Check(sent == signatureBase58, "sendTransaction");

Console.WriteLine("AOT smoke passed.");

static void Check(bool condition, string what)
{
    if (condition)
    {
        Console.WriteLine($"ok: {what}");
        return;
    }

    throw new InvalidOperationException($"AOT smoke failed: {what}.");
}

/// <summary>Answers each JSON-RPC method with a canned response, keyed on the request body.</summary>
internal sealed class CannedRpcHandler : HttpMessageHandler
{
    private const string LatestBlockhashJson =
        """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":{"blockhash":"CktRuQ2mttgRGkXJtyksdKHjUdc2C4TgDzyB98oEzy8","lastValidBlockHeight":100}},"id":1}""";

    private const string AccountInfoJson =
        """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":{"data":["AQID","base64"],"executable":false,"lamports":42,"owner":"11111111111111111111111111111111","rentEpoch":0}},"id":1}""";

    private const string ProgramAccountsJson =
        """{"jsonrpc":"2.0","result":[],"id":1}""";

    private const string SimulateTransactionJson =
        """{"jsonrpc":"2.0","result":{"context":{"slot":2,"apiVersion":"3.1.0"},"value":{"err":null,"logs":[],"accounts":[{"lamports":42,"data":["AQID","base64"],"owner":"11111111111111111111111111111111","executable":false,"rentEpoch":0,"space":3}],"unitsConsumed":10,"loadedAccountsDataSize":3,"returnData":{"programId":"11111111111111111111111111111111","data":["BAU=","base64"]},"innerInstructions":[],"fee":5000}},"id":1}""";

    private const string AgGenesisCertificateJson =
        """{"jsonrpc":"2.0","result":null,"id":1}""";

    private const string VersionedTransactionJson =
        """{"jsonrpc":"2.0","result":{"slot":3,"blockTime":null,"transaction":["gQECAw==","base64"],"meta":null,"version":1},"id":1}""";

    private const string ParsedVersionedTransactionJson =
        """{"jsonrpc":"2.0","result":{"slot":3,"blockTime":null,"transaction":{"signatures":["5VERv8NMvzbJMEkV8xnrLkEaWRtSz9CosKDYjCJjBRnbJLgp8uirBgmQpjKhoR4tjF3ZpRzrFmBV6UjKdiSZkQUW"],"message":{"accountKeys":[],"instructions":[],"recentBlockhash":"CktRuQ2mttgRGkXJtyksdKHjUdc2C4TgDzyB98oEzy8","transactionConfig":{"priorityFee":5000,"computeUnitLimit":200000,"loadedAccountsDataSizeLimit":65536,"heapSize":32768}}},"meta":null,"version":1},"id":1}""";

    private const string SendTransactionJson =
        """{"jsonrpc":"2.0","result":"5VERv8NMvzbJMEkV8xnrLkEaWRtSz9CosKDYjCJjBRnbJLgp8uirBgmQpjKhoR4tjF3ZpRzrFmBV6UjKdiSZkQUW","id":1}""";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var json = body switch
        {
            _ when body.Contains("getLatestBlockhash") => LatestBlockhashJson,
            _ when body.Contains("getProgramAccounts") => ProgramAccountsJson,
            _ when body.Contains("getAccountInfo") => AccountInfoJson,
            _ when body.Contains("simulateTransaction") => SimulateTransactionJson,
            _ when body.Contains("getAgGenesisCert") => AgGenesisCertificateJson,
            _ when body.Contains("getTransaction") && body.Contains("jsonParsed") => ParsedVersionedTransactionJson,
            _ when body.Contains("getTransaction") => VersionedTransactionJson,
            _ when body.Contains("sendTransaction") => SendTransactionJson,
            _ => throw new InvalidOperationException($"Unexpected RPC request: {body}")
        };

        return new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }
}
