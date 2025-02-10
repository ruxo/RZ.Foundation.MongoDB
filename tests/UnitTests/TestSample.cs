using JetBrains.Annotations;
using MongoDB.Driver;
using RZ.Foundation.MongoDb;
using RZ.Foundation.MongoDb.Migration;

namespace UnitTests;

public readonly record struct Address(string Country, string Zip);

public record Customer(string Name, Address Address, uint Version, DateTimeOffset Updated, Guid Id = default) : IHaveKey<Guid>, ICanUpdateVersion<Customer>
{
    public Customer WithVersion(DateTimeOffset updated, uint next)
        => this with { Updated = updated, Version = next };
}

[PublicAPI]
public static class TestSample
{
    public static readonly Guid UnusedGuid1 = new("503461E9-969B-4847-8CC7-F920370C39AB");
    public static readonly Guid UnusedGuid2 = new("1F9E1596-3484-44C2-B0C3-B55CF69CAAD1");

    public static readonly DateTimeOffset NewYear2024 = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public static readonly string UniqueZip = "11111";

    public static readonly Customer JohnDoe = new("John Doe", new Address("TH", UniqueZip), 1, new(2020, 1, 1, 17, 0, 0, TimeSpan.Zero), new("0B8D9631-720A-46B7-8C95-F55B4EC520A4"));
    public static readonly Customer JaneDoe = new("Jane Doe", new Address("TH", "10000"), 2, new(2020, 1, 31, 17, 0, 0, TimeSpan.Zero), new("B823FD8C-C995-4B64-96FB-D83BEBAAD21D"));

    public static readonly Customer HelloWorld = new("Hello World", new Address("US", "10000"), 1, new(2020, 2, 13, 17, 0, 0, TimeSpan.Zero), new("711ca94d-239c-4e67-81c9-1f2f155b3f43"));

    /// New kid on the block, never be in the database before
    public static readonly Customer NewKid = new("New Kid", new Address("US", "10000"), 1, new(2020, 2, 13, 17, 0, 0, TimeSpan.Zero), new("BADA86E1-5EAD-4FAE-BDA6-D2C108A7BD9B"));

    public static IMongoCollection<Customer> ImportSamples(this IMongoCollection<Customer> collection)
    {
        collection.InsertMany([ JohnDoe, JaneDoe, HelloWorld ]);
        return collection;
    }

    public static IMongoDatabase ImportSamples(this IMongoDatabase db)
    {
        db.Collection<Customer>().ImportSamples();
        return db;
    }
}