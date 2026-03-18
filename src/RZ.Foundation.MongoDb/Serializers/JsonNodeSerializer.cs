using System.Text.Json.Nodes;
using JetBrains.Annotations;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace RZ.Foundation.MongoDb.Serializers;

/// <summary>
/// Custom BSON serializer for System.Text.Json.Nodes.JsonNode
/// </summary>
[PublicAPI]
public sealed class JsonNodeSerializer : SerializerBase<JsonNode>
{
    public override JsonNode Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args) {
        var reader = context.Reader;

        return reader.CurrentBsonType switch {
            BsonType.Document   => DeserializeDocument(context, args),
            BsonType.Array      => DeserializeArray(context, args),
            BsonType.String     => JsonValue.Create(reader.ReadString()),
            BsonType.Boolean    => JsonValue.Create(reader.ReadBoolean()),
            BsonType.Int32      => JsonValue.Create(reader.ReadInt32()),
            BsonType.Int64      => JsonValue.Create(reader.ReadInt64()),
            BsonType.Double     => JsonValue.Create(reader.ReadDouble()),
            BsonType.Decimal128 => JsonValue.Create((decimal)reader.ReadDecimal128()),
            BsonType.DateTime   => JsonValue.Create(reader.ReadDateTime()),
            BsonType.ObjectId   => JsonValue.Create(reader.ReadObjectId().ToString()),
            BsonType.Null       => SideEffect<JsonNode?>(_ => reader.ReadNull())(null)!,

            // Add more cases as needed for other BsonTypes
            BsonType.EndOfDocument or BsonType.Binary or BsonType.Undefined or BsonType.RegularExpression
             or BsonType.JavaScript or BsonType.Symbol or BsonType.JavaScriptWithScope or BsonType.Timestamp
             or BsonType.MinKey or BsonType.MaxKey => throw new NotSupportedException($"Unsupported BsonType: {reader.CurrentBsonType}"),

            _ => throw new NotSupportedException($"Unsupported BsonType: {reader.CurrentBsonType}")
        };
    }

    JsonObject DeserializeDocument(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var reader = context.Reader;
        reader.ReadStartDocument();
        var jsonObject = new JsonObject();

        while (reader.ReadBsonType() != BsonType.EndOfDocument)
        {
            var name = reader.ReadName();
            var value = Deserialize(context, args);
            jsonObject.Add(name, value);
        }

        reader.ReadEndDocument();
        return jsonObject;
    }

    JsonArray DeserializeArray(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var reader = context.Reader;
        reader.ReadStartArray();
        var jsonArray = new JsonArray();

        while (reader.ReadBsonType() != BsonType.EndOfDocument)
        {
            var value = Deserialize(context, args);
            jsonArray.Add(value);
        }

        reader.ReadEndArray();
        return jsonArray;
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, JsonNode? value)
    {
        if (value is null)
        {
            context.Writer.WriteNull();
            return;
        }

        switch (value)
        {
            case JsonObject jsonObject:
                SerializeJsonObject(context, args, jsonObject);
                break;

            case JsonArray jsonArray:
                SerializeJsonArray(context, args, jsonArray);
                break;

            case JsonValue jsonValue:
                SerializeJsonValue(context.Writer, jsonValue);
                break;

            default:
                throw new NotSupportedException($"Unsupported JsonNode type: {value.GetType()}");
        }
    }

    void SerializeJsonObject(BsonSerializationContext context, BsonSerializationArgs args, JsonObject jsonObject)
    {
        var writer = context.Writer;
        writer.WriteStartDocument();

        foreach (var property in jsonObject)
        {
            writer.WriteName(property.Key);
            Serialize(context, args, property.Value);
        }

        writer.WriteEndDocument();
    }

    void SerializeJsonArray(BsonSerializationContext context, BsonSerializationArgs args, JsonArray jsonArray)
    {
        var writer = context.Writer;
        writer.WriteStartArray();

        foreach (var item in jsonArray)
        {
            Serialize(context, args, item);
        }

        writer.WriteEndArray();
    }

    void SerializeJsonValue(IBsonWriter writer, JsonValue jsonValue)
    {
        // If value is null, write null
        if (jsonValue.GetValueKind() == System.Text.Json.JsonValueKind.Null)
            writer.WriteNull();
        else if (jsonValue.TryGetValue<string>(out var stringValue))
            writer.WriteString(stringValue);
        else if (jsonValue.TryGetValue<bool>(out var boolValue))
            writer.WriteBoolean(boolValue);
        else if (jsonValue.TryGetValue<int>(out var intValue))
            writer.WriteInt32(intValue);
        else if (jsonValue.TryGetValue<long>(out var longValue))
            writer.WriteInt64(longValue);
        else if (jsonValue.TryGetValue<double>(out var doubleValue))
            writer.WriteDouble(doubleValue);
        else if (jsonValue.TryGetValue<decimal>(out var decimalValue))
            writer.WriteDecimal128(decimalValue);
        else if (jsonValue.TryGetValue<DateTime>(out var dateTimeValue))
            writer.WriteDateTime(dateTimeValue.ToUniversalTime().Ticks / 10000);
        else{
            // Fall back to string for other types
            writer.WriteString(jsonValue.ToJsonString());
        }
    }
}