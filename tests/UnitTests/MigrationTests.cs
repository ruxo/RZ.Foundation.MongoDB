using System.Reflection;
using MongoDB.Driver;
using MongoDBMigrations;
using Version = MongoDBMigrations.Version;

namespace UnitTests;

public class MigrationTests
{
    [Fact(DisplayName = "Initialize migration")]
    public void InitializeMigration() {
        var mdb = MockDb.StartDb();

        var client = new MongoClient(mdb.ConnectionString);
        var migration = new MigrationEngine()
                       .UseDatabase(client, "test")
                       .UseAssembly(Assembly.GetExecutingAssembly())
                       .UseSchemeValidation(enabled: false);

        migration.Run();

        migration.Run(new Version(0, 0, 1));
    }
}