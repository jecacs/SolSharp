// The Native AOT smoke check: published with PublishAot and run as a native binary in CI, it exercises
// the paths that would break if reflection crept back in - the source-generated JSON pipeline (request
// serialization, response envelopes, the hand-written converters), Ed25519 signing, transaction
// compilation and serialization, and PDA/ATA derivation. Everything runs offline against a canned
// HTTP handler; any failure throws and fails the job through the non-zero exit code.
using System.Net;
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
var signature = keypair.Sign(message);
Check(keypair.PublicKey.Verify(message, signature), "sign/verify round-trip");

var recipient = PublicKey.Parse(systemProgram);
var transactionMessage = new TransactionBuilder()
    .SetFeePayer(keypair.PublicKey)
    .SetRecentBlockhash(blockhash)
    .AddInstruction(SystemProgram.Transfer(keypair.PublicKey, recipient, 1_000_000))
    .BuildMessage();

var transaction = Transaction.Create(transactionMessage).Sign(keypair);
var wire = transaction.Serialize();
Check(wire.Length == transaction.GetSerializedLength(), "transaction serialization length");

var (pda, _) = ProgramDerivedAddress.FindProgramAddress(["smoke"u8.ToArray()], recipient);
var ata = AssociatedTokenAccount.GetAddress(keypair.PublicKey, recipient);
Check(pda != default && ata != default, "PDA/ATA derivation");

using var http = new HttpClient(new CannedRpcHandler()) { BaseAddress = new Uri("http://localhost") };
var client = new SolanaRpcClient(http);

var latest = await client.GetLatestBlockhashAsync();
Check(latest.Blockhash == blockhash, "getLatestBlockhash");

var account = await client.GetAccountInfoAsync(recipient);
Check(account is { Lamports: 42, Data: [1, 2, 3] }, "getAccountInfo");

var sent = await client.SendTransactionAsync(wire);
Check(sent == signatureBase58, "sendTransaction");

Console.WriteLine("AOT smoke passed.");

static void Check(bool condition, string what)
{
    if (!condition)
        throw new InvalidOperationException($"AOT smoke failed: {what}.");

    Console.WriteLine($"ok: {what}");
}

/// <summary>Answers each JSON-RPC method with a canned response, keyed on the request body.</summary>
internal sealed class CannedRpcHandler : HttpMessageHandler
{
    private const string LatestBlockhashJson =
        """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":{"blockhash":"CktRuQ2mttgRGkXJtyksdKHjUdc2C4TgDzyB98oEzy8","lastValidBlockHeight":100}},"id":1}""";

    private const string AccountInfoJson =
        """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":{"data":["AQID","base64"],"executable":false,"lamports":42,"owner":"11111111111111111111111111111111","rentEpoch":0}},"id":1}""";

    private const string SendTransactionJson =
        """{"jsonrpc":"2.0","result":"5VERv8NMvzbJMEkV8xnrLkEaWRtSz9CosKDYjCJjBRnbJLgp8uirBgmQpjKhoR4tjF3ZpRzrFmBV6UjKdiSZkQUW","id":1}""";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var json = body switch
        {
            _ when body.Contains("getLatestBlockhash") => LatestBlockhashJson,
            _ when body.Contains("getAccountInfo") => AccountInfoJson,
            _ when body.Contains("sendTransaction") => SendTransactionJson,
            _ => throw new InvalidOperationException($"Unexpected RPC request: {body}")
        };

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }
}
