using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Rpc.Models;
using SolSharp.Rpc.Models.Parsed;
using SolSharp.Rpc.Protocol;

namespace SolSharp.Rpc.Tests;

public static class RpcTransactionVersionTests
{
    [TestFixture]
    public sealed class FromNumber
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(byte.MaxValue)]
        public void U8Value_PreservesNumber(int number)
        {
            // Act
            var version = RpcTransactionVersion.FromNumber((byte)number);

            // Assert
            version.IsLegacy.Should().BeFalse();
            version.Number.Should().Be((byte)number);
        }
    }

    [TestFixture]
    public sealed class Read
    {
        [Test]
        public void LegacyString_ProducesLegacyVariant()
        {
            // Act
            var version = JsonSerializer.Deserialize<RpcTransactionVersion>("\"legacy\"", RpcJson.Options);

            // Assert
            version.Should().Be(RpcTransactionVersion.Legacy);
            version.IsLegacy.Should().BeTrue();
            version.Number.Should().BeNull();
        }

        [TestCase("0", 0)]
        [TestCase("1", 1)]
        [TestCase("255", byte.MaxValue)]
        public void U8Number_ProducesNumericVariant(string json, int expected)
        {
            // Act
            var version = JsonSerializer.Deserialize<RpcTransactionVersion>(json, RpcJson.Options);

            // Assert
            version.Should().Be(RpcTransactionVersion.FromNumber((byte)expected));
        }

        [TestCase("null")]
        [TestCase("true")]
        [TestCase("{}")]
        [TestCase("[]")]
        [TestCase("\"Legacy\"")]
        [TestCase("\"0\"")]
        [TestCase("-1")]
        [TestCase("256")]
        [TestCase("1.0")]
        public void ValueOutsideClosedUnion_ThrowsJsonException(string json)
        {
            // Act
            Action act = () => JsonSerializer.Deserialize<RpcTransactionVersion>(json, RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>().WithMessage("*legacy*u8 integer*");
        }
    }

    [TestFixture]
    public sealed class Write
    {
        [Test]
        public void Variants_ProduceExactWireValues()
        {
            // Act
            var legacy = JsonSerializer.Serialize(RpcTransactionVersion.Legacy, RpcJson.Options);
            var numeric = JsonSerializer.Serialize(RpcTransactionVersion.FromNumber(byte.MaxValue), RpcJson.Options);

            // Assert
            legacy.Should().Be("\"legacy\"");
            numeric.Should().Be("255");
        }

        [Test]
        public void UninitializedValue_ThrowsJsonException()
        {
            // Act
            Action act = static () => JsonSerializer.Serialize(default(RpcTransactionVersion), RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>().WithMessage("*uninitialized transaction version*");
        }
    }
}

public static class TransactionResponseJsonTests
{
    [TestFixture]
    public sealed class Deserialize
    {
        [TestCase("{}")]
        [TestCase("{\"version\":null}")]
        public void OptionalVersionAbsentOrNull_IsPreservedAsNull(string json)
        {
            // Arrange
            using var document = JsonDocument.Parse(json);
            var version = document.RootElement.TryGetProperty("version", out var member)
                ? ",\"version\":" + member.GetRawText()
                : string.Empty;
            var responseJson =
                "{\"slot\":0,\"blockTime\":null,\"transaction\":[\"\",\"base64\"],\"meta\":null" +
                version + "}";

            // Act
            var response = JsonSerializer.Deserialize<TransactionResponse>(responseJson, RpcJson.Options);

            // Assert
            response!.Version.Should().BeNull();
        }

        [TestCase("{\"status\":{\"Ok\":null},\"fee\":0,\"preBalances\":[],\"postBalances\":[]}")]
        [TestCase("{\"err\":null,\"fee\":0,\"preBalances\":[],\"postBalances\":[]}")]
        [TestCase("{\"err\":null,\"status\":{\"Ok\":null},\"preBalances\":[],\"postBalances\":[]}")]
        [TestCase("{\"err\":null,\"status\":{\"Ok\":null},\"fee\":0,\"postBalances\":[]}")]
        [TestCase("{\"err\":null,\"status\":{\"Ok\":null},\"fee\":0,\"preBalances\":[]}")]
        [TestCase("{\"err\":null,\"status\":null,\"fee\":0,\"preBalances\":[],\"postBalances\":[]}")]
        [TestCase("{\"err\":null,\"status\":{\"Ok\":null},\"fee\":0,\"preBalances\":null,\"postBalances\":[]}")]
        [TestCase("{\"err\":null,\"status\":{\"Ok\":null},\"fee\":0,\"preBalances\":[],\"postBalances\":null}")]
        public void MalformedCoreMetadata_ThrowsJsonException(string metadata)
        {
            // Arrange
            var json =
                "{\"slot\":0,\"blockTime\":null,\"transaction\":[\"\",\"base64\"],\"meta\":" +
                metadata + "}";

            // Act
            Action act = () => JsonSerializer.Deserialize<TransactionResponse>(json, RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>();
        }

        [TestCase("{\"data\":[\"\",\"base64\"]}")]
        [TestCase("{\"programId\":\"11111111111111111111111111111111\"}")]
        [TestCase("{\"programId\":\"11111111111111111111111111111111\",\"data\":null}")]
        public void MalformedReturnData_ThrowsJsonException(string returnData)
        {
            // Arrange
            var json =
                "{\"slot\":0,\"blockTime\":null,\"transaction\":[\"\",\"base64\"],\"meta\":{\"err\":null," +
                "\"status\":{\"Ok\":null},\"fee\":0,\"preBalances\":[],\"postBalances\":[],\"returnData\":" +
                returnData + "}}";

            // Act
            Action act = () => JsonSerializer.Deserialize<TransactionResponse>(json, RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>();
        }

        [TestCase("{\"writable\":[]}")]
        [TestCase("{\"readonly\":[]}")]
        [TestCase("{\"writable\":null,\"readonly\":[]}")]
        [TestCase("{\"writable\":[],\"readonly\":null}")]
        public void MalformedLoadedAddresses_ThrowsJsonException(string loadedAddresses)
        {
            // Arrange
            var json =
                "{\"slot\":0,\"blockTime\":null,\"transaction\":[\"\",\"base64\"],\"meta\":{\"err\":null," +
                "\"status\":{\"Ok\":null},\"fee\":0,\"preBalances\":[],\"postBalances\":[],\"loadedAddresses\":" +
                loadedAddresses + "}}";

            // Act
            Action act = () => JsonSerializer.Deserialize<TransactionResponse>(json, RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>();
        }

        [TestCase("{\"lamports\":0,\"postBalance\":0}")]
        [TestCase("{\"pubkey\":\"11111111111111111111111111111111\",\"postBalance\":0}")]
        [TestCase("{\"pubkey\":\"11111111111111111111111111111111\",\"lamports\":0}")]
        public void RewardMissingMandatoryField_ThrowsJsonException(string reward)
        {
            // Arrange
            var json =
                "{\"slot\":0,\"blockTime\":null,\"transaction\":[\"\",\"base64\"],\"meta\":{\"err\":null," +
                "\"status\":{\"Ok\":null},\"fee\":0,\"preBalances\":[],\"postBalances\":[],\"rewards\":[" +
                reward + "]}}";

            // Act
            Action act = () => JsonSerializer.Deserialize<TransactionResponse>(json, RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>();
        }

        [Test]
        public void RewardOptionsAbsent_RemainNull()
        {
            // Arrange
            const string json =
                """{"slot":0,"blockTime":null,"transaction":["","base64"],"meta":{"err":null,"status":{"Ok":null},"fee":0,"preBalances":[],"postBalances":[],"rewards":[{"pubkey":"11111111111111111111111111111111","lamports":0,"postBalance":0}]}}""";

            // Act
            var response = JsonSerializer.Deserialize<TransactionResponse>(json, RpcJson.Options);

            // Assert
            var reward = response!.Meta!.Rewards.Should().ContainSingle().Subject;
            reward.RewardType.Should().BeNull();
            reward.Commission.Should().BeNull();
            reward.CommissionBps.Should().BeNull();
        }

        [TestCase("Fee")]
        [TestCase("Rent")]
        [TestCase("Staking")]
        [TestCase("Voting")]
        [TestCase("DeactivatedStake")]
        public void PinnedRewardType_IsAccepted(string rewardType)
        {
            // Arrange
            var json =
                "{\"slot\":0,\"blockTime\":null,\"transaction\":[\"\",\"base64\"],\"meta\":{\"err\":null," +
                "\"status\":{\"Ok\":null},\"fee\":0,\"preBalances\":[],\"postBalances\":[],\"rewards\":[{" +
                "\"pubkey\":\"11111111111111111111111111111111\",\"lamports\":0,\"postBalance\":0,\"rewardType\":\"" +
                rewardType + "\"}]}}";

            // Act
            var response = JsonSerializer.Deserialize<TransactionResponse>(json, RpcJson.Options);

            // Assert
            response!.Meta!.Rewards.Should().ContainSingle().Which.RewardType.Should().Be(rewardType);
        }

        [Test]
        public void UnknownRewardType_ThrowsJsonException()
        {
            // Arrange
            const string json =
                """{"slot":0,"blockTime":null,"transaction":["","base64"],"meta":{"err":null,"status":{"Ok":null},"fee":0,"preBalances":[],"postBalances":[],"rewards":[{"pubkey":"11111111111111111111111111111111","lamports":0,"postBalance":0,"rewardType":"Unknown"}]}}""";

            // Act
            Action act = static () => JsonSerializer.Deserialize<TransactionResponse>(json, RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>().WithMessage("*reward type*");
        }

        [TestCase("{\"blockTime\":null,\"transaction\":[\"\",\"base64\"],\"meta\":null}")]
        [TestCase("{\"slot\":0,\"transaction\":[\"\",\"base64\"],\"meta\":null}")]
        [TestCase("{\"slot\":0,\"blockTime\":null,\"meta\":null}")]
        [TestCase("{\"slot\":0,\"blockTime\":null,\"transaction\":null,\"meta\":null}")]
        [TestCase("{\"slot\":0,\"blockTime\":null,\"transaction\":[\"\",\"base64\"]}")]
        public void MissingMandatoryOuterField_ThrowsJsonException(string json)
        {
            // Act
            Action act = () => JsonSerializer.Deserialize<TransactionResponse>(json, RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>();
        }

        [Test]
        public void ExactIntegerWidths_AcceptBoundaries()
        {
            // Arrange
            const string json =
                """{"slot":0,"blockTime":null,"transaction":["","base64"],"meta":{"err":null,"status":{"Ok":null},"fee":0,"preBalances":[],"postBalances":[],"preTokenBalances":[{"accountIndex":255,"mint":"11111111111111111111111111111111","uiTokenAmount":{"amount":"0","decimals":0,"uiAmount":0,"uiAmountString":"0"}}],"innerInstructions":[{"index":255,"instructions":[{"programIdIndex":255,"accounts":[255],"data":"","stackHeight":4294967295}]}]}}""";

            // Act
            var response = JsonSerializer.Deserialize<TransactionResponse>(json, RpcJson.Options);

            // Assert
            response!.Meta!.PreTokenBalances.Should().ContainSingle().Which.AccountIndex.Should().Be(byte.MaxValue);
            var group = response.Meta.InnerInstructions.Should().ContainSingle().Subject;
            group.Index.Should().Be(byte.MaxValue);
            var instruction = group.Instructions.Should().ContainSingle().Subject;
            instruction.ProgramIdIndex.Should().Be(byte.MaxValue);
            instruction.Accounts.Should().Equal(byte.MaxValue);
            instruction.StackHeight.Should().Be(uint.MaxValue);
        }

        [TestCase("\"preTokenBalances\":[{\"accountIndex\":256,\"mint\":\"11111111111111111111111111111111\",\"uiTokenAmount\":{\"amount\":\"0\",\"decimals\":0,\"uiAmount\":0,\"uiAmountString\":\"0\"}}]")]
        [TestCase("\"innerInstructions\":[{\"index\":256,\"instructions\":[]}]")]
        [TestCase("\"innerInstructions\":[{\"index\":0,\"instructions\":[{\"programIdIndex\":256,\"accounts\":[],\"data\":\"\",\"stackHeight\":null}]}]")]
        [TestCase("\"innerInstructions\":[{\"index\":0,\"instructions\":[{\"programIdIndex\":0,\"accounts\":[256],\"data\":\"\",\"stackHeight\":null}]}]")]
        [TestCase("\"innerInstructions\":[{\"index\":0,\"instructions\":[{\"programIdIndex\":0,\"accounts\":[],\"data\":\"\",\"stackHeight\":4294967296}]}]")]
        public void IntegerWidthOverflow_ThrowsJsonException(string member)
        {
            // Arrange
            var json =
                "{\"slot\":0,\"blockTime\":null,\"transaction\":[\"\",\"base64\"],\"meta\":{\"err\":null," +
                "\"status\":{\"Ok\":null},\"fee\":0,\"preBalances\":[],\"postBalances\":[]," + member + "}}";

            // Act
            Action act = () => JsonSerializer.Deserialize<TransactionResponse>(json, RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>();
        }

        [TestCase("{}")]
        [TestCase("{\"index\":0}")]
        [TestCase("{\"instructions\":[]}")]
        [TestCase("{\"index\":0,\"instructions\":null}")]
        [TestCase("{\"index\":0,\"instructions\":[null]}")]
        [TestCase("{\"index\":0,\"instructions\":[{}]}")]
        [TestCase("{\"index\":0,\"instructions\":[{\"accounts\":[],\"data\":\"\",\"stackHeight\":null}]}")]
        [TestCase("{\"index\":0,\"instructions\":[{\"programIdIndex\":0,\"data\":\"\",\"stackHeight\":null}]}")]
        [TestCase("{\"index\":0,\"instructions\":[{\"programIdIndex\":0,\"accounts\":[],\"stackHeight\":null}]}")]
        [TestCase("{\"index\":0,\"instructions\":[{\"programIdIndex\":0,\"accounts\":[],\"data\":\"\"}]}")]
        [TestCase("{\"index\":0,\"instructions\":[{\"programIdIndex\":0,\"accounts\":null,\"data\":\"\",\"stackHeight\":null}]}")]
        [TestCase("{\"index\":0,\"instructions\":[{\"programIdIndex\":0,\"accounts\":[],\"data\":null,\"stackHeight\":null}]}")]
        public void MalformedCompiledInnerInstructions_ThrowsJsonException(string group)
        {
            // Arrange
            var json =
                "{\"slot\":0,\"blockTime\":null,\"transaction\":[\"\",\"base64\"],\"meta\":{\"err\":null," +
                "\"status\":{\"Ok\":null},\"fee\":0,\"preBalances\":[],\"postBalances\":[]," +
                "\"innerInstructions\":[" + group + "]}}";

            // Act
            Action act = () => JsonSerializer.Deserialize<TransactionResponse>(json, RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>();
        }

        [TestCase("{\"Ok\":null}", "{\"InstructionError\":[0,\"Custom\"]}")]
        [TestCase("{\"Err\":{\"InstructionError\":[0,\"Custom\"]}}", "null")]
        [TestCase("{\"Err\":null}", "{\"InstructionError\":[0,\"Custom\"]}")]
        [TestCase("{\"Err\":{\"InstructionError\":[1,\"Custom\"]}}", "{\"InstructionError\":[0,\"Custom\"]}")]
        [TestCase("{}", "null")]
        [TestCase("{\"Ok\":null,\"Err\":{}}", "null")]
        public void InconsistentStatusAndError_ThrowsJsonException(string status, string error)
        {
            // Arrange
            var json =
                "{\"slot\":0,\"blockTime\":null,\"transaction\":[\"\",\"base64\"],\"meta\":{\"err\":" +
                error + ",\"status\":" + status + ",\"fee\":0,\"preBalances\":[],\"postBalances\":[]}}";

            // Act
            Action act = () => JsonSerializer.Deserialize<TransactionResponse>(json, RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>();
        }

        [TestCase("{\"accountIndex\":0,\"uiTokenAmount\":{\"amount\":\"0\",\"decimals\":0,\"uiAmount\":0,\"uiAmountString\":\"0\"}}")]
        [TestCase("{\"mint\":\"11111111111111111111111111111111\",\"uiTokenAmount\":{\"amount\":\"0\",\"decimals\":0,\"uiAmount\":0,\"uiAmountString\":\"0\"}}")]
        [TestCase("{\"accountIndex\":0,\"mint\":\"11111111111111111111111111111111\"}")]
        [TestCase("{\"accountIndex\":0,\"mint\":\"11111111111111111111111111111111\",\"uiTokenAmount\":null}")]
        public void TokenBalanceMissingMandatoryField_ThrowsJsonException(string balance)
        {
            // Arrange
            var json =
                "{\"slot\":0,\"blockTime\":null,\"transaction\":[\"\",\"base64\"],\"meta\":{\"err\":null," +
                "\"status\":{\"Ok\":null},\"fee\":0,\"preBalances\":[],\"postBalances\":[],\"preTokenBalances\":[" +
                balance + "]}}";

            // Act
            Action act = () => JsonSerializer.Deserialize<TransactionResponse>(json, RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>();
        }

        [TestCase("\"preTokenBalances\":[null]")]
        [TestCase("\"postTokenBalances\":[null]")]
        [TestCase("\"innerInstructions\":[null]")]
        [TestCase("\"logMessages\":[null]")]
        [TestCase("\"rewards\":[null]")]
        public void NullEntryInOptionalMetadataCollection_ThrowsJsonException(string member)
        {
            // Arrange
            var json =
                "{\"slot\":0,\"blockTime\":null,\"transaction\":[\"\",\"base64\"],\"meta\":{\"err\":null," +
                "\"status\":{\"Ok\":null},\"fee\":0,\"preBalances\":[],\"postBalances\":[]," + member + "}}";

            // Act
            Action act = () => JsonSerializer.Deserialize<TransactionResponse>(json, RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>();
        }
    }
}

public static class ParsedTransactionJsonTests
{
    [TestFixture]
    public sealed class Read
    {
        [TestCase("{}")]
        [TestCase("{\"transaction\":null}")]
        [TestCase("{\"transaction\":[]}")]
        [TestCase("{\"transaction\":{}}")]
        [TestCase("{\"transaction\":{\"signatures\":null,\"message\":{\"accountKeys\":[],\"instructions\":[],\"recentBlockhash\":\"\"}}}")]
        [TestCase("{\"transaction\":{\"signatures\":[null],\"message\":{\"accountKeys\":[],\"instructions\":[],\"recentBlockhash\":\"\"}}}")]
        [TestCase("{\"transaction\":{\"signatures\":[]}}")]
        [TestCase("{\"transaction\":{\"signatures\":[],\"message\":null}}")]
        public void MalformedTransactionEnvelope_ThrowsJsonException(string json)
        {
            // Act
            Action act = () => JsonSerializer.Deserialize<ParsedTransaction>(json, RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>();
        }

        [TestCase("{\"instructions\":[],\"recentBlockhash\":\"\"}")]
        [TestCase("{\"accountKeys\":[],\"recentBlockhash\":\"\"}")]
        [TestCase("{\"accountKeys\":[],\"instructions\":[]}")]
        [TestCase("{\"accountKeys\":null,\"instructions\":[],\"recentBlockhash\":\"\"}")]
        [TestCase("{\"accountKeys\":[null],\"instructions\":[],\"recentBlockhash\":\"\"}")]
        [TestCase("{\"accountKeys\":[],\"instructions\":null,\"recentBlockhash\":\"\"}")]
        [TestCase("{\"accountKeys\":[],\"instructions\":[null],\"recentBlockhash\":\"\"}")]
        [TestCase("{\"accountKeys\":[],\"instructions\":[],\"recentBlockhash\":null}")]
        public void MalformedMessage_ThrowsJsonException(string message)
        {
            // Arrange
            var json = "{\"transaction\":{\"signatures\":[],\"message\":" + message + "},\"meta\":null}";

            // Act
            Action act = () => JsonSerializer.Deserialize<ParsedTransaction>(json, RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>();
        }

        [TestCase("{}")]
        [TestCase("{\"accountKey\":\"11111111111111111111111111111111\",\"readonlyIndexes\":[]}")]
        [TestCase("{\"accountKey\":\"11111111111111111111111111111111\",\"writableIndexes\":[]}")]
        [TestCase("{\"accountKey\":\"11111111111111111111111111111111\",\"writableIndexes\":null,\"readonlyIndexes\":[]}")]
        [TestCase("{\"accountKey\":\"11111111111111111111111111111111\",\"writableIndexes\":[],\"readonlyIndexes\":null}")]
        public void MalformedAddressTableLookup_ThrowsJsonException(string lookup)
        {
            // Arrange
            var json =
                "{\"transaction\":{\"signatures\":[],\"message\":{\"accountKeys\":[],\"instructions\":[]," +
                "\"recentBlockhash\":\"\",\"addressTableLookups\":[" + lookup + "]}},\"meta\":null}";

            // Act
            Action act = () => JsonSerializer.Deserialize<ParsedTransaction>(json, RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>();
        }

        [TestCase("\"Legacy\"")]
        [TestCase("\"v0\"")]
        [TestCase("-1")]
        [TestCase("256")]
        [TestCase("1.0")]
        public void InvalidVersion_ThrowsJsonException(string version)
        {
            // Arrange
            var json =
                "{\"transaction\":{\"signatures\":[],\"message\":{\"accountKeys\":[],\"instructions\":[]," +
                "\"recentBlockhash\":\"\"}},\"meta\":null,\"version\":" + version + "}";

            // Act
            Action act = () => JsonSerializer.Deserialize<ParsedTransaction>(json, RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>();
        }

        [TestCase("{}")]
        [TestCase("{\"version\":null}")]
        public void OptionalVersionAbsentOrNull_IsPreservedAsNull(string optionalMember)
        {
            // Arrange
            using var optional = JsonDocument.Parse(optionalMember);
            var version = optional.RootElement.TryGetProperty("version", out var member)
                ? ",\"version\":" + member.GetRawText()
                : string.Empty;
            var json =
                "{\"transaction\":{\"signatures\":[],\"message\":{\"accountKeys\":[],\"instructions\":[],\"recentBlockhash\":\"\"}}" +
                ",\"meta\":null" + version + "}";

            // Act
            var transaction = JsonSerializer.Deserialize<ParsedTransaction>(json, RpcJson.Options);

            // Assert
            transaction!.Version.Should().BeNull();
        }

        [Test]
        public void MetadataMemberMissing_ThrowsJsonException()
        {
            // Arrange
            const string json =
                """{"transaction":{"signatures":[],"message":{"accountKeys":[],"instructions":[],"recentBlockhash":""}}}""";

            // Act
            Action act = static () => JsonSerializer.Deserialize<ParsedTransaction>(json, RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>().WithMessage("*metadata member*");
        }

        [TestCase("\"slot\":0")]
        [TestCase("\"blockTime\":null")]
        [TestCase("\"slot\":null,\"blockTime\":null")]
        [TestCase("\"slot\":-1,\"blockTime\":null")]
        [TestCase("\"slot\":\"0\",\"blockTime\":null")]
        [TestCase("\"slot\":0,\"blockTime\":\"0\"")]
        [TestCase("\"transactionIndex\":null")]
        [TestCase("\"transactionIndex\":-1")]
        [TestCase("\"transactionIndex\":4294967296")]
        public void MalformedPositionMember_ThrowsJsonException(string members)
        {
            // Arrange
            var json =
                "{\"transaction\":{\"signatures\":[],\"message\":{\"accountKeys\":[],\"instructions\":[]," +
                "\"recentBlockhash\":\"\"}},\"meta\":null," + members + "}";

            // Act
            Action act = () => JsonSerializer.Deserialize<ParsedTransaction>(json, RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>();
        }

        [TestCase("{\"signer\":false,\"writable\":false,\"source\":null}")]
        [TestCase("{\"pubkey\":\"11111111111111111111111111111111\",\"writable\":false,\"source\":null}")]
        [TestCase("{\"pubkey\":\"11111111111111111111111111111111\",\"signer\":false,\"source\":null}")]
        [TestCase("{\"pubkey\":\"11111111111111111111111111111111\",\"signer\":false,\"writable\":false}")]
        [TestCase("{\"pubkey\":\"11111111111111111111111111111111\",\"signer\":false,\"writable\":false,\"source\":\"static\"}")]
        public void MalformedAccountKey_ThrowsJsonException(string accountKey)
        {
            // Arrange
            var json =
                "{\"transaction\":{\"signatures\":[],\"message\":{\"accountKeys\":[" + accountKey +
                "],\"instructions\":[],\"recentBlockhash\":\"\"}},\"meta\":null}";

            // Act
            Action act = () => JsonSerializer.Deserialize<ParsedTransaction>(json, RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>();
        }

        [TestCase("{}")]
        [TestCase("{\"priorityFee\":null,\"computeUnitLimit\":null,\"loadedAccountsDataSizeLimit\":null}")]
        public void MalformedTransactionConfig_ThrowsJsonException(string config)
        {
            // Arrange
            var json =
                "{\"transaction\":{\"signatures\":[],\"message\":{\"accountKeys\":[],\"instructions\":[]," +
                "\"recentBlockhash\":\"\",\"transactionConfig\":" + config + "}},\"meta\":null}";

            // Act
            Action act = () => JsonSerializer.Deserialize<ParsedTransaction>(json, RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>();
        }

        [Test]
        public void NullTransactionConfigOptions_ArePreserved()
        {
            // Arrange
            const string json =
                """{"transaction":{"signatures":[],"message":{"accountKeys":[],"instructions":[],"recentBlockhash":"","transactionConfig":{"priorityFee":null,"computeUnitLimit":null,"loadedAccountsDataSizeLimit":null,"heapSize":null}}},"meta":null}""";

            // Act
            var transaction = JsonSerializer.Deserialize<ParsedTransaction>(json, RpcJson.Options);

            // Assert
            transaction!.Message.TransactionConfig.Should().NotBeNull();
            transaction.Message.TransactionConfig!.PriorityFee.Should().BeNull();
            transaction.Message.TransactionConfig.ComputeUnitLimit.Should().BeNull();
            transaction.Message.TransactionConfig.LoadedAccountsDataSizeLimit.Should().BeNull();
            transaction.Message.TransactionConfig.HeapSize.Should().BeNull();
        }

        [TestCase("{}")]
        [TestCase("{\"program\":\"system\",\"programId\":\"11111111111111111111111111111111\",\"parsed\":{},\"stackHeight\":null,\"accounts\":[],\"data\":\"\"}")]
        [TestCase("{\"programId\":\"11111111111111111111111111111111\",\"accounts\":[],\"data\":\"\",\"stackHeight\":null,\"program\":\"system\",\"parsed\":{}}")]
        [TestCase("{\"program\":\"system\",\"programId\":\"11111111111111111111111111111111\",\"parsed\":{}}")]
        [TestCase("{\"programId\":\"11111111111111111111111111111111\",\"accounts\":[],\"data\":\"\"}")]
        [TestCase("{\"program\":\"system\",\"parsed\":{},\"stackHeight\":null}")]
        [TestCase("{\"program\":\"system\",\"programId\":\"11111111111111111111111111111111\",\"stackHeight\":null}")]
        public void MalformedInstructionUnion_ThrowsJsonException(string instruction)
        {
            // Arrange
            var json =
                "{\"transaction\":{\"signatures\":[],\"message\":{\"accountKeys\":[],\"instructions\":[" +
                instruction + "],\"recentBlockhash\":\"\"}},\"meta\":null}";

            // Act
            Action act = () => JsonSerializer.Deserialize<ParsedTransaction>(json, RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>();
        }

        [Test]
        public void BothInstructionBranches_PreserveExactValues()
        {
            // Arrange
            const string json =
                """{"transaction":{"signatures":[],"message":{"accountKeys":[],"instructions":[{"program":"system","programId":"11111111111111111111111111111111","parsed":null,"stackHeight":4294967295},{"programId":"11111111111111111111111111111111","accounts":["11111111111111111111111111111111"],"data":"","stackHeight":null}],"recentBlockhash":""}},"meta":null}""";

            // Act
            var transaction = JsonSerializer.Deserialize<ParsedTransaction>(json, RpcJson.Options);

            // Assert
            var parsed = transaction!.Message.Instructions[0];
            parsed.Program.Should().Be("system");
            parsed.Parsed.Should().NotBeNull();
            parsed.Parsed!.Info.ValueKind.Should().Be(JsonValueKind.Null);
            parsed.StackHeight.Should().Be(uint.MaxValue);
            var partial = transaction.Message.Instructions[1];
            partial.Program.Should().BeNull();
            partial.Parsed.Should().BeNull();
            partial.Accounts.Should().ContainSingle();
            partial.Data.Should().BeEmpty();
        }

        [Test]
        public void ArbitraryParsedObjectShape_IsPreservedWithoutProjectionFailure()
        {
            // Arrange
            const string json =
                """{"transaction":{"signatures":[],"message":{"accountKeys":[],"instructions":[{"program":"custom","programId":"11111111111111111111111111111111","parsed":{"type":42,"info":{"x":1}},"stackHeight":null}],"recentBlockhash":""}},"meta":null}""";

            // Act
            var transaction = JsonSerializer.Deserialize<ParsedTransaction>(json, RpcJson.Options);

            // Assert
            var parsed = transaction!.Message.Instructions.Should().ContainSingle().Subject.Parsed!;
            parsed.Type.Should().BeEmpty();
            parsed.Info.GetProperty("type").GetInt32().Should().Be(42);
            parsed.Info.GetProperty("info").GetProperty("x").GetInt32().Should().Be(1);
        }

        [TestCase("{}")]
        [TestCase("{\"index\":0}")]
        [TestCase("{\"instructions\":[]}")]
        [TestCase("{\"index\":0,\"instructions\":null}")]
        [TestCase("{\"index\":0,\"instructions\":[null]}")]
        [TestCase("{\"index\":256,\"instructions\":[]}")]
        public void MalformedParsedInnerInstructions_ThrowsJsonException(string group)
        {
            // Arrange
            var json =
                "{\"err\":null,\"status\":{\"Ok\":null},\"fee\":0,\"preBalances\":[],\"postBalances\":[]," +
                "\"innerInstructions\":[" + group + "]}";

            // Act
            Action act = () => JsonSerializer.Deserialize<ParsedTransactionMeta>(json, RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>();
        }

        [Test]
        public void ParsedInnerInstructionWidths_AcceptBoundaries()
        {
            // Arrange
            const string json =
                """{"err":null,"status":{"Ok":null},"fee":0,"preBalances":[],"postBalances":[],"innerInstructions":[{"index":255,"instructions":[{"program":"system","programId":"11111111111111111111111111111111","parsed":{},"stackHeight":4294967295}]}]}""";

            // Act
            var metadata = JsonSerializer.Deserialize<ParsedTransactionMeta>(json, RpcJson.Options);

            // Assert
            var group = metadata!.InnerInstructions.Should().ContainSingle().Subject;
            group.Index.Should().Be(byte.MaxValue);
            group.Instructions.Should().ContainSingle().Which.StackHeight.Should().Be(uint.MaxValue);
        }

        [Test]
        public void ParsedInstructionStackHeightOverflow_ThrowsJsonException()
        {
            // Arrange
            const string json =
                """{"transaction":{"signatures":[],"message":{"accountKeys":[],"instructions":[{"program":"system","programId":"11111111111111111111111111111111","parsed":{},"stackHeight":4294967296}],"recentBlockhash":""}},"meta":null}""";

            // Act
            Action act = static () => JsonSerializer.Deserialize<ParsedTransaction>(json, RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>();
        }

        [Test]
        public void InconsistentParsedStatusAndError_ThrowsJsonException()
        {
            // Arrange
            const string json =
                """{"err":{"InstructionError":[0,"Custom"]},"status":{"Ok":null},"fee":0,"preBalances":[],"postBalances":[]}""";

            // Act
            Action act = static () => JsonSerializer.Deserialize<ParsedTransactionMeta>(json, RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>();
        }

        [TestCase("\"preTokenBalances\":[null]")]
        [TestCase("\"postTokenBalances\":[null]")]
        [TestCase("\"innerInstructions\":[null]")]
        [TestCase("\"logMessages\":[null]")]
        [TestCase("\"rewards\":[null]")]
        public void NullEntryInOptionalParsedMetadataCollection_ThrowsJsonException(string member)
        {
            // Arrange
            var json =
                "{\"err\":null,\"status\":{\"Ok\":null},\"fee\":0,\"preBalances\":[],\"postBalances\":[]," +
                member + "}";

            // Act
            Action act = () => JsonSerializer.Deserialize<ParsedTransactionMeta>(json, RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>();
        }
    }
}

public static class ParsedBlockJsonTests
{
    [TestFixture]
    public sealed class Deserialize
    {
        [TestCase("{}")]
        [TestCase("{\"previousBlockhash\":\"\",\"parentSlot\":0,\"blockTime\":null,\"blockHeight\":null,\"transactions\":[]}")]
        [TestCase("{\"blockhash\":\"\",\"parentSlot\":0,\"blockTime\":null,\"blockHeight\":null,\"transactions\":[]}")]
        [TestCase("{\"blockhash\":\"\",\"previousBlockhash\":\"\",\"blockTime\":null,\"blockHeight\":null,\"transactions\":[]}")]
        [TestCase("{\"blockhash\":\"\",\"previousBlockhash\":\"\",\"parentSlot\":0,\"blockHeight\":null,\"transactions\":[]}")]
        [TestCase("{\"blockhash\":\"\",\"previousBlockhash\":\"\",\"parentSlot\":0,\"blockTime\":null,\"transactions\":[]}")]
        [TestCase("{\"blockhash\":\"\",\"previousBlockhash\":\"\",\"parentSlot\":0,\"blockTime\":null,\"blockHeight\":null,\"transactions\":null}")]
        [TestCase("{\"blockhash\":\"\",\"previousBlockhash\":\"\",\"parentSlot\":0,\"blockTime\":null,\"blockHeight\":null,\"transactions\":[null]}")]
        public void MalformedBlock_ThrowsJsonException(string json)
        {
            // Act
            Action act = () => JsonSerializer.Deserialize<ParsedBlock>(json, RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>();
        }

        [Test]
        public void NullableBlockFieldsPresent_ArePreserved()
        {
            // Arrange
            const string json =
                """{"blockhash":"","previousBlockhash":"","parentSlot":0,"blockTime":null,"blockHeight":null,"transactions":[]}""";

            // Act
            var block = JsonSerializer.Deserialize<ParsedBlock>(json, RpcJson.Options);

            // Assert
            block!.BlockTime.Should().BeNull();
            block.BlockHeight.Should().BeNull();
            block.Transactions.Should().BeEmpty();
        }
    }
}
