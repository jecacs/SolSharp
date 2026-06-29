using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Tests;

public static class SolanaRpcClientParsedTransactionTests
{
    private const string SystemId = "11111111111111111111111111111111";
    private const string TokenId = "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA";
    private const string Usdc = "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v";
    private const string Alice = "3x9az88Dkbxa6tkKByxqEn7jBTJCJCD4dVvou49L24ET";
    private const string Bob = "9jLkNAaW9E47LQMHvjohy2uAAyr1331bAxgJKFRU7wF6";
    private const string Owner = "67vHA8qZGCJKw1UNGUJZME4MwEWDRGWzp7MGvsut43A8";
    private const string SrcAta = "GE3oyzjSohCRBKq75a2ug4pDFx7GGKJXsz1GfQr836uP";
    private const string DstAta = "Gdc1ZJMLFqN3f3xMDu8Sm6KJ7NNQzJ2GbmLBKUU7pCs4";
    private const string Amm = "7QMhYQAPfkoURcrQFxgHKXbipaYL4Sj34kweHx3d3J67";
    private const string V0Writable = "5CTyWy6H2GiE3mNp8aJjUVqu7eH2JRXbDqNhpVPkRBvo";
    private const string V0Readonly = "Fydr76JtKYEyFnzTvoEJbKpfgfaWC29XSPWibA4SzEFu";

    private static (SolanaRpcClient Client, FakeHttpMessageHandler Handler) Make(string responseJson)
    {
        var handler = new FakeHttpMessageHandler(responseJson);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        return (new SolanaRpcClient(http), handler);
    }

    private static PublicKey Key(string base58) => PublicKey.Parse(base58);

    [TestFixture]
    public sealed class GetParsedTransactionAsync
    {
        [Test]
        public async Task ParsesSystemTransfer()
        {
            // Arrange
            var (client, handler) = Make(Transfer);

            // Act
            var tx = await client.GetParsedTransactionAsync("sig1aaaa");

            // Assert
            tx.Should().NotBeNull();
            tx!.Slot.Should().Be(250000000);
            tx.BlockTime.Should().Be(1700000000);
            tx.Signatures.Should().ContainSingle().Which.Should().Be("sig1aaaa");
            tx.Message.RecentBlockhash.Should().Be("RBh1transfer1111111111111111111111111111111");

            var sender = tx.Message.AccountKeys[0];
            sender.Pubkey.Should().Be(Key(Alice));
            sender.Signer.Should().BeTrue();
            sender.Writable.Should().BeTrue();
            sender.Source.Should().Be("transaction");

            var ix = tx.Message.Instructions.Should().ContainSingle().Subject;
            ix.ProgramId.Should().Be(Key(SystemId));
            ix.Program.Should().Be("system");
            ix.Parsed.Should().NotBeNull();
            ix.Parsed!.Type.Should().Be("transfer");
            ix.Parsed.Info.GetProperty("destination").GetString().Should().Be(Bob);
            ix.Parsed.Info.GetProperty("lamports").GetInt64().Should().Be(1000000);
            ix.Accounts.Should().BeNull();
            ix.Data.Should().BeNull();

            tx.Meta.Should().NotBeNull();
            tx.Meta!.Fee.Should().Be(5000);
            tx.Meta.IsError.Should().BeFalse();
            tx.Meta.Error.Should().BeNull();
            tx.Meta.LogMessages.Should().HaveCount(2);

            handler.CapturedRequestBody.Should().Contain("getTransaction");
            handler.CapturedRequestBody.Should().Contain("jsonParsed");
        }

        [Test]
        public async Task ParsesInnerInstructionsAndTokenBalances()
        {
            // Arrange
            var (client, _) = Make(Inner);

            // Act
            var tx = await client.GetParsedTransactionAsync("sig2bbbb");

            // Assert
            var top = tx!.Message.Instructions.Should().ContainSingle().Subject;
            top.ProgramId.Should().Be(Key(Amm));
            top.Program.Should().BeNull();
            top.Parsed.Should().BeNull();
            top.Accounts.Should().Equal(Key(SrcAta), Key(DstAta), Key(Owner));
            top.Data.Should().Be("3Bxs4h24hBtQy9rw");

            var inner = tx.Meta!.InnerInstructions.Should().ContainSingle().Subject;
            inner.Index.Should().Be(0);
            var cpi = inner.Instructions.Should().ContainSingle().Subject;
            cpi.ProgramId.Should().Be(Key(TokenId));
            cpi.Parsed!.Type.Should().Be("transferChecked");

            var pre = tx.Meta.PreTokenBalances.Should().ContainSingle().Subject;
            pre.AccountIndex.Should().Be(1);
            pre.Mint.Should().Be(Key(Usdc));
            pre.Owner.Should().Be(Key(Owner));
            pre.ProgramId.Should().Be(Key(TokenId));
            pre.UiTokenAmount.Amount.Should().Be("5000000");
            pre.UiTokenAmount.Decimals.Should().Be(6);
            pre.UiTokenAmount.UiAmountString.Should().Be("5");

            tx.Meta.PostTokenBalances.Should().HaveCount(2);
        }

        [Test]
        public async Task DecodesVersionedTransactionWithLoadedAddresses()
        {
            // Arrange
            var (client, _) = Make(Versioned);

            // Act
            var tx = await client.GetParsedTransactionAsync("sig3cccc");

            // Assert
            tx.Should().NotBeNull();
            tx!.Meta!.LoadedAddresses.Should().NotBeNull();
            tx.Meta.LoadedAddresses!.Writable.Should().ContainSingle().Which.Should().Be(Key(V0Writable));
            tx.Meta.LoadedAddresses.Readonly.Should().ContainSingle().Which.Should().Be(Key(V0Readonly));
            tx.Message.AccountKeys.Should().Contain(account => account.Source == "lookupTable");
        }

        [Test]
        public async Task ReturnsNullWhenNotFound()
        {
            // Arrange
            var (client, _) = Make(NotFound);

            // Act & Assert
            (await client.GetParsedTransactionAsync("missing")).Should().BeNull();
        }

        [Test]
        public async Task ToleratesMissingOptionalFields()
        {
            // Arrange
            var (client, _) = Make(Malformed);

            // Act
            var tx = await client.GetParsedTransactionAsync("sigX");

            // Assert
            tx.Should().NotBeNull();
            tx!.Slot.Should().BeNull();
            tx.BlockTime.Should().BeNull();
            tx.Meta.Should().BeNull();
            tx.Message.Instructions.Should().BeEmpty();
            tx.Message.AccountKeys.Should().ContainSingle().Which.Source.Should().BeNull();
        }

        [Test]
        public async Task ParsesMemoInstructionWhoseParsedIsAString()
        {
            // Arrange
            var (client, _) = Make(Memo);

            // Act
            var tx = await client.GetParsedTransactionAsync("sigMemo");

            // Assert
            var ix = tx!.Message.Instructions.Should().ContainSingle().Subject;
            ix.Program.Should().Be("spl-memo");
            ix.Parsed.Should().NotBeNull();
            ix.Parsed!.Type.Should().BeEmpty();                 // spl-memo carries no action type
            ix.Parsed.Info.GetString().Should().Be("gm wagmi"); // the memo text rides on Info, not dropped
            ix.Accounts.Should().BeNull();
        }

        [Test]
        public async Task SurfacesTypedErrorForFailedTransaction()
        {
            // Arrange
            var (client, _) = Make(Failed);

            // Act
            var tx = await client.GetParsedTransactionAsync("sigFail");

            // Assert
            tx!.Meta!.IsError.Should().BeTrue();
            var error = tx.Meta.Error!;
            error.Kind.Should().Be("InstructionError");
            error.InstructionIndex.Should().Be(0);
            error.InstructionError!.CustomCode.Should().Be(6001);
        }

        [Test]
        public async Task ParsesMemoInvokedAsInnerInstruction()
        {
            // Arrange
            var (client, _) = Make(InnerMemo);

            // Act
            var tx = await client.GetParsedTransactionAsync("sigInnerMemo");

            // Assert - the string-shaped parsed is tolerated at the inner (CPI) level too
            var inner = tx!.Meta!.InnerInstructions.Should().ContainSingle().Subject;
            var cpi = inner.Instructions.Should().ContainSingle().Subject;
            cpi.Program.Should().Be("spl-memo");
            cpi.Parsed!.Type.Should().BeEmpty();
            cpi.Parsed.Info.GetString().Should().Be("cpi memo");
        }
    }

    [TestFixture]
    public sealed class GetParsedBlockAsync
    {
        [Test]
        public async Task ParsesTransactionsAndFillsSlotAndBlockTime()
        {
            // Arrange
            var (client, handler) = Make(BlockJson);

            // Act
            var block = await client.GetParsedBlockAsync(250000000);

            // Assert
            block.Should().NotBeNull();
            block!.Blockhash.Should().Be("BHash5block11111111111111111111111111111111");
            block.ParentSlot.Should().Be(249999999);
            block.BlockHeight.Should().Be(123456);
            block.BlockTime.Should().Be(1700000005);
            block.Transactions.Should().HaveCount(2);

            var first = block.Transactions[0];
            first.Slot.Should().Be(250000000);       // patched from the requested slot
            first.BlockTime.Should().Be(1700000005);  // patched from the block
            first.Message.Instructions[0].Parsed!.Type.Should().Be("transfer");

            block.Transactions[1].Meta.Should().BeNull();

            handler.CapturedRequestBody.Should().Contain("getBlock");
            handler.CapturedRequestBody.Should().Contain("jsonParsed");
            handler.CapturedRequestBody.Should().Contain("full");
        }
    }

    private const string Transfer =
        """{"jsonrpc":"2.0","result":{"slot":250000000,"blockTime":1700000000,"transaction":{"signatures":["sig1aaaa"],"message":{"accountKeys":[{"pubkey":"3x9az88Dkbxa6tkKByxqEn7jBTJCJCD4dVvou49L24ET","signer":true,"writable":true,"source":"transaction"},{"pubkey":"9jLkNAaW9E47LQMHvjohy2uAAyr1331bAxgJKFRU7wF6","signer":false,"writable":true,"source":"transaction"},{"pubkey":"11111111111111111111111111111111","signer":false,"writable":false,"source":"transaction"}],"instructions":[{"program":"system","programId":"11111111111111111111111111111111","parsed":{"type":"transfer","info":{"source":"3x9az88Dkbxa6tkKByxqEn7jBTJCJCD4dVvou49L24ET","destination":"9jLkNAaW9E47LQMHvjohy2uAAyr1331bAxgJKFRU7wF6","lamports":1000000}},"stackHeight":null}],"recentBlockhash":"RBh1transfer1111111111111111111111111111111"}},"meta":{"err":null,"fee":5000,"preBalances":[100000000,0,1],"postBalances":[98995000,1000000,1],"innerInstructions":[],"logMessages":["Program 11111111111111111111111111111111 invoke [1]","Program 11111111111111111111111111111111 success"],"preTokenBalances":[],"postTokenBalances":[],"loadedAddresses":{"writable":[],"readonly":[]}},"version":"legacy"},"id":1}""";

    private const string Inner =
        """{"jsonrpc":"2.0","result":{"slot":250000001,"blockTime":1700000001,"transaction":{"signatures":["sig2bbbb"],"message":{"accountKeys":[{"pubkey":"67vHA8qZGCJKw1UNGUJZME4MwEWDRGWzp7MGvsut43A8","signer":true,"writable":true,"source":"transaction"},{"pubkey":"GE3oyzjSohCRBKq75a2ug4pDFx7GGKJXsz1GfQr836uP","signer":false,"writable":true,"source":"transaction"},{"pubkey":"Gdc1ZJMLFqN3f3xMDu8Sm6KJ7NNQzJ2GbmLBKUU7pCs4","signer":false,"writable":true,"source":"transaction"},{"pubkey":"7QMhYQAPfkoURcrQFxgHKXbipaYL4Sj34kweHx3d3J67","signer":false,"writable":false,"source":"transaction"},{"pubkey":"TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA","signer":false,"writable":false,"source":"transaction"}],"instructions":[{"programId":"7QMhYQAPfkoURcrQFxgHKXbipaYL4Sj34kweHx3d3J67","accounts":["GE3oyzjSohCRBKq75a2ug4pDFx7GGKJXsz1GfQr836uP","Gdc1ZJMLFqN3f3xMDu8Sm6KJ7NNQzJ2GbmLBKUU7pCs4","67vHA8qZGCJKw1UNGUJZME4MwEWDRGWzp7MGvsut43A8"],"data":"3Bxs4h24hBtQy9rw","stackHeight":null}],"recentBlockhash":"RBh2inner11111111111111111111111111111111111"}},"meta":{"err":null,"fee":5000,"preBalances":[100000000,2039280,2039280,1,1],"postBalances":[99995000,2039280,2039280,1,1],"innerInstructions":[{"index":0,"instructions":[{"program":"spl-token","programId":"TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA","parsed":{"type":"transferChecked","info":{"source":"GE3oyzjSohCRBKq75a2ug4pDFx7GGKJXsz1GfQr836uP","destination":"Gdc1ZJMLFqN3f3xMDu8Sm6KJ7NNQzJ2GbmLBKUU7pCs4","mint":"EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v","tokenAmount":{"amount":"1000000","decimals":6,"uiAmount":1.0,"uiAmountString":"1"},"authority":"67vHA8qZGCJKw1UNGUJZME4MwEWDRGWzp7MGvsut43A8"}},"stackHeight":2}]}],"logMessages":["Program X invoke [1]","Program Tokenkeg success"],"preTokenBalances":[{"accountIndex":1,"mint":"EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v","owner":"67vHA8qZGCJKw1UNGUJZME4MwEWDRGWzp7MGvsut43A8","programId":"TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA","uiTokenAmount":{"amount":"5000000","decimals":6,"uiAmount":5.0,"uiAmountString":"5"}}],"postTokenBalances":[{"accountIndex":1,"mint":"EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v","owner":"67vHA8qZGCJKw1UNGUJZME4MwEWDRGWzp7MGvsut43A8","programId":"TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA","uiTokenAmount":{"amount":"4000000","decimals":6,"uiAmount":4.0,"uiAmountString":"4"}},{"accountIndex":2,"mint":"EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v","owner":"9jLkNAaW9E47LQMHvjohy2uAAyr1331bAxgJKFRU7wF6","programId":"TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA","uiTokenAmount":{"amount":"1000000","decimals":6,"uiAmount":1.0,"uiAmountString":"1"}}],"loadedAddresses":{"writable":[],"readonly":[]}},"version":0},"id":1}""";

    private const string Versioned =
        """{"jsonrpc":"2.0","result":{"slot":250000002,"blockTime":1700000002,"transaction":{"signatures":["sig3cccc"],"message":{"accountKeys":[{"pubkey":"3x9az88Dkbxa6tkKByxqEn7jBTJCJCD4dVvou49L24ET","signer":true,"writable":true,"source":"transaction"},{"pubkey":"7QMhYQAPfkoURcrQFxgHKXbipaYL4Sj34kweHx3d3J67","signer":false,"writable":false,"source":"transaction"},{"pubkey":"5CTyWy6H2GiE3mNp8aJjUVqu7eH2JRXbDqNhpVPkRBvo","signer":false,"writable":true,"source":"lookupTable"},{"pubkey":"Fydr76JtKYEyFnzTvoEJbKpfgfaWC29XSPWibA4SzEFu","signer":false,"writable":false,"source":"lookupTable"}],"instructions":[{"programId":"7QMhYQAPfkoURcrQFxgHKXbipaYL4Sj34kweHx3d3J67","accounts":["5CTyWy6H2GiE3mNp8aJjUVqu7eH2JRXbDqNhpVPkRBvo","Fydr76JtKYEyFnzTvoEJbKpfgfaWC29XSPWibA4SzEFu"],"data":"ABCD","stackHeight":null}],"recentBlockhash":"RBh3v01111111111111111111111111111111111111"}},"meta":{"err":null,"fee":5000,"preBalances":[1,1,1,1],"postBalances":[1,1,1,1],"innerInstructions":[],"logMessages":[],"preTokenBalances":[],"postTokenBalances":[],"loadedAddresses":{"writable":["5CTyWy6H2GiE3mNp8aJjUVqu7eH2JRXbDqNhpVPkRBvo"],"readonly":["Fydr76JtKYEyFnzTvoEJbKpfgfaWC29XSPWibA4SzEFu"]}},"version":0},"id":1}""";

    private const string Memo =
        """{"jsonrpc":"2.0","result":{"slot":250000003,"blockTime":1700000003,"transaction":{"signatures":["sigMemo"],"message":{"accountKeys":[{"pubkey":"3x9az88Dkbxa6tkKByxqEn7jBTJCJCD4dVvou49L24ET","signer":true,"writable":true,"source":"transaction"},{"pubkey":"MemoSq4gqABAXKb96qnH8TysNcWxMyWCqXgDLGmfcHr","signer":false,"writable":false,"source":"transaction"}],"instructions":[{"program":"spl-memo","programId":"MemoSq4gqABAXKb96qnH8TysNcWxMyWCqXgDLGmfcHr","parsed":"gm wagmi","stackHeight":null}],"recentBlockhash":"RBhMemo1111111111111111111111111111111111111"}},"meta":{"err":null,"fee":5000,"preBalances":[1,1],"postBalances":[1,1],"innerInstructions":[],"logMessages":[],"preTokenBalances":[],"postTokenBalances":[],"loadedAddresses":{"writable":[],"readonly":[]}},"version":"legacy"},"id":1}""";

    private const string Failed =
        """{"jsonrpc":"2.0","result":{"slot":250000004,"blockTime":1700000004,"transaction":{"signatures":["sigFail"],"message":{"accountKeys":[{"pubkey":"3x9az88Dkbxa6tkKByxqEn7jBTJCJCD4dVvou49L24ET","signer":true,"writable":true,"source":"transaction"},{"pubkey":"11111111111111111111111111111111","signer":false,"writable":false,"source":"transaction"}],"instructions":[{"program":"system","programId":"11111111111111111111111111111111","parsed":{"type":"transfer","info":{"lamports":1}},"stackHeight":null}],"recentBlockhash":"RBhFail1111111111111111111111111111111111111"}},"meta":{"err":{"InstructionError":[0,{"Custom":6001}]},"fee":5000,"preBalances":[1,1],"postBalances":[1,1],"innerInstructions":[],"logMessages":["Program failed"],"preTokenBalances":[],"postTokenBalances":[],"loadedAddresses":{"writable":[],"readonly":[]}},"version":"legacy"},"id":1}""";

    private const string InnerMemo =
        """{"jsonrpc":"2.0","result":{"slot":250000005,"blockTime":1700000005,"transaction":{"signatures":["sigInnerMemo"],"message":{"accountKeys":[{"pubkey":"67vHA8qZGCJKw1UNGUJZME4MwEWDRGWzp7MGvsut43A8","signer":true,"writable":true,"source":"transaction"},{"pubkey":"7QMhYQAPfkoURcrQFxgHKXbipaYL4Sj34kweHx3d3J67","signer":false,"writable":false,"source":"transaction"}],"instructions":[{"programId":"7QMhYQAPfkoURcrQFxgHKXbipaYL4Sj34kweHx3d3J67","accounts":["67vHA8qZGCJKw1UNGUJZME4MwEWDRGWzp7MGvsut43A8"],"data":"3Bxs","stackHeight":null}],"recentBlockhash":"RBhInner111111111111111111111111111111111111"}},"meta":{"err":null,"fee":5000,"preBalances":[1,1],"postBalances":[1,1],"innerInstructions":[{"index":0,"instructions":[{"program":"spl-memo","programId":"MemoSq4gqABAXKb96qnH8TysNcWxMyWCqXgDLGmfcHr","parsed":"cpi memo","stackHeight":2}]}],"logMessages":[],"preTokenBalances":[],"postTokenBalances":[],"loadedAddresses":{"writable":[],"readonly":[]}},"version":0},"id":1}""";

    private const string NotFound = """{"jsonrpc":"2.0","result":null,"id":1}""";

    private const string Malformed =
        """{"jsonrpc":"2.0","result":{"transaction":{"signatures":["sigX"],"message":{"accountKeys":[{"pubkey":"3x9az88Dkbxa6tkKByxqEn7jBTJCJCD4dVvou49L24ET","signer":true,"writable":true}],"instructions":[],"recentBlockhash":"RBh4mal111111111111111111111111111111111111"}}},"id":1}""";

    private const string BlockJson =
        """{"jsonrpc":"2.0","result":{"blockhash":"BHash5block11111111111111111111111111111111","previousBlockhash":"BHash6parent1111111111111111111111111111111","parentSlot":249999999,"blockHeight":123456,"blockTime":1700000005,"transactions":[{"transaction":{"signatures":["sigA"],"message":{"accountKeys":[{"pubkey":"3x9az88Dkbxa6tkKByxqEn7jBTJCJCD4dVvou49L24ET","signer":true,"writable":true,"source":"transaction"},{"pubkey":"9jLkNAaW9E47LQMHvjohy2uAAyr1331bAxgJKFRU7wF6","signer":false,"writable":true,"source":"transaction"},{"pubkey":"11111111111111111111111111111111","signer":false,"writable":false,"source":"transaction"}],"instructions":[{"program":"system","programId":"11111111111111111111111111111111","parsed":{"type":"transfer","info":{"lamports":42}},"stackHeight":null}],"recentBlockhash":"RBh7blktx111111111111111111111111111111111"}},"meta":{"err":null,"fee":5000,"preBalances":[1,1,1],"postBalances":[1,1,1],"innerInstructions":[],"logMessages":[],"preTokenBalances":[],"postTokenBalances":[],"loadedAddresses":{"writable":[],"readonly":[]}},"version":"legacy"},{"transaction":{"signatures":["sigB"],"message":{"accountKeys":[{"pubkey":"67vHA8qZGCJKw1UNGUJZME4MwEWDRGWzp7MGvsut43A8","signer":true,"writable":true,"source":"transaction"}],"instructions":[],"recentBlockhash":"RBh8blktx211111111111111111111111111111111"}},"meta":null,"version":0}]},"id":1}""";
}
