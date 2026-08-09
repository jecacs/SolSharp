using System.Text.Json;
using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;
using SolSharp.Rpc.Protocol;

namespace SolSharp.Rpc.Models.Parsed;

/// <summary>Reads the two exact, mutually exclusive shapes of an Agave parsed instruction.</summary>
internal sealed class ParsedInstructionJsonConverter : JsonConverter<ParsedInstruction>
{
    public override ParsedInstruction Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind is not JsonValueKind.Object)
            throw new JsonException("A parsed instruction must be an object.");

        var hasProgram = root.TryGetProperty("program", out var program);
        var hasParsed = root.TryGetProperty("parsed", out var parsed);
        var hasAccounts = root.TryGetProperty("accounts", out var accounts);
        var hasData = root.TryGetProperty("data", out var data);

        var programId = ReadProgramId(root, options);
        var stackHeight = ReadStackHeight(root);

        if (hasProgram || hasParsed)
        {
            if (!hasProgram || !hasParsed || hasAccounts || hasData ||
                program.ValueKind is not JsonValueKind.String)
            {
                throw new JsonException("A parsed instruction must carry exactly program, programId, parsed, and stackHeight.");
            }

            var parsedInfo = parsed.ValueKind is JsonValueKind.Null
                ? new ParsedInstructionInfo { Info = parsed.Clone() }
                : parsed.Deserialize(options.GetTypeInfo<ParsedInstructionInfo>())
                    ?? throw new JsonException("A parsed instruction must carry a parsed JSON value.");

            return new ParsedInstruction
            {
                Program = program.GetString(),
                ProgramId = programId,
                Parsed = parsedInfo,
                StackHeight = stackHeight
            };
        }

        if (!hasAccounts || !hasData || accounts.ValueKind is not JsonValueKind.Array ||
            data.ValueKind is not JsonValueKind.String)
        {
            throw new JsonException("A partially decoded instruction must carry programId, accounts, data, and stackHeight.");
        }

        var parsedAccounts = accounts.Deserialize(options.GetTypeInfo<IReadOnlyList<PublicKey>>())
            ?? throw new JsonException("A partially decoded instruction must carry a non-null accounts array.");

        return new ParsedInstruction
        {
            ProgramId = programId,
            Accounts = parsedAccounts,
            Data = data.GetString(),
            StackHeight = stackHeight
        };
    }

    public override void Write(Utf8JsonWriter writer, ParsedInstruction value, JsonSerializerOptions options)
        => throw new NotSupportedException("ParsedInstruction is decoded from node responses and is not serialized.");

    private static PublicKey ReadProgramId(JsonElement root, JsonSerializerOptions options)
    {
        if (!root.TryGetProperty("programId", out var programId) || programId.ValueKind is not JsonValueKind.String)
            throw new JsonException("A parsed instruction must carry a non-null programId.");

        return programId.Deserialize(options.GetTypeInfo<PublicKey>());
    }

    private static uint? ReadStackHeight(JsonElement root)
    {
        if (!root.TryGetProperty("stackHeight", out var stackHeight))
            throw new JsonException("A parsed instruction must carry a stackHeight member.");

        if (stackHeight.ValueKind is JsonValueKind.Null)
            return null;

        if (stackHeight.ValueKind is JsonValueKind.Number && stackHeight.TryGetUInt32(out var value))
            return value;

        throw new JsonException("A parsed instruction stackHeight must be an unsigned 32-bit integer or null.");
    }
}
