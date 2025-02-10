using MongoDB.Driver;
using MongoDBMigrations;
using RZ.Foundation.MongoDb.Migration;
using Version = MongoDBMigrations.Version;

namespace UnitTests.Migration;

public class TestMigration : IMigration
{
    public void Up(IMongoDatabase database, IClientSessionHandle session) {
        database.Build<Customer>()
                .WithSchema(RZ.Foundation.MongoDb.Migration.Migration.Validation.Requires<Customer>())
                .UniqueIndex("Name", b => b.Ascending(x => x.Name))
                .Run(session);
    }
    public void Down(IMongoDatabase database, IClientSessionHandle session) {
        database.DropCollection(session, nameof(Customer));
    }

    public Version Version => new(0, 0, 1);
    public string Name => "Test migration";
}

public class MigrationStep2 : IMigration
{
    public void Up(IMongoDatabase database, IClientSessionHandle session) {
        database.Collection<Customer>().DropIndex("Name");
    }
    public void Down(IMongoDatabase database, IClientSessionHandle session) {
        database.Collection<Customer>().CreateUniqueIndex("Name", b => b.Ascending(x => x.Name));
    }

    public Version Version => new(0, 0, 2);
    public string Name => "Migration step 2";
}