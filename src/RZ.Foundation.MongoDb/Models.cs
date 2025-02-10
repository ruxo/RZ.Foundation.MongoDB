global using VersionType = uint;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace RZ.Foundation.MongoDb;

/// <summary>
/// Explicit collection name for MongoDB
/// </summary>
/// <param name="name">Name of collection</param>
[AttributeUsage(AttributeTargets.Class), PublicAPI, ExcludeFromCodeCoverage]
public class CollectionNameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

[PublicAPI]
public interface IHaveKey<out T>
{
    T Id { get; }
}

[PublicAPI]
public interface IHaveVersion {
    DateTimeOffset Updated { get; }
    VersionType Version { get; }
}

[PublicAPI]
public interface ICanUpdateVersion<out T> : IHaveVersion
{
    T WithVersion(DateTimeOffset updated, uint next);
}