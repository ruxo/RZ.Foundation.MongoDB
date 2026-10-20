using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using JetBrains.Annotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using RZ.Foundation.Types;

namespace RZ.Foundation.MongoDb;

[PublicAPI]
public static class MongoHelper
{
    static readonly ConcurrentDictionary<Type, string> CollectionNameCache = new();

    public static string GetCollectionName<T>()
        => CollectionNameCache.GetOrAdd(typeof(T), t => t.GetCustomAttribute<CollectionNameAttribute>()?.Name ?? t.Name);

    [ExcludeFromCodeCoverage]
    public static void SetupMongoStandardMappings(bool useLegacyGuid = false) {
        BsonSerializer.RegisterSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));

        if (!useLegacyGuid)
            BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

        // FUTURE: Remove once it's stable
        // BsonSerializer.RegisterSerializer(new Serializers.JsonNodeSerializer());

        var pack = new ConventionPack{ new EnumRepresentationConvention(BsonType.String) };
        ConventionRegistry.Register("EnumString", pack, _ => true);
    }

    // Mongo server error code WriteConflict: another write changed the document first
    const int WRITE_CONFLICT = 112;

    [Pure]
    public static ErrorInfo? TryInterpretDatabaseError(Exception e)
        => e switch {
            MongoWriteException { WriteError.Category: ServerErrorCategory.DuplicateKey }
                => new ErrorInfo(StandardErrorCodes.Duplication, "Either data identity, name, or both are already existed", e.ToString()),
            // a conflicting write inside a transaction comes back as a command error, not as a write error
            MongoCommandException { Code: WRITE_CONFLICT } or MongoWriteException { WriteError.Code: WRITE_CONFLICT }
                => ErrorInfo.New(StandardErrorCodes.RaceCondition, "Data has changed externally", e.ToString()),
            MongoException
                => new ErrorInfo(StandardErrorCodes.DatabaseTransactionError, e.Message, e.ToString()),
            _ => null
        };

    [Pure]
    public static ErrorInfo InterpretDatabaseError(Exception e)
        => TryInterpretDatabaseError(e) ?? ErrorFrom.Exception(e);
}