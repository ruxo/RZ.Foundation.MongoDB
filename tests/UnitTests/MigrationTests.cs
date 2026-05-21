using JetBrains.Annotations;
using MongoDB.Driver;
using MongoDBMigrations;
using Assembly = System.Reflection.Assembly;
using Version = MongoDBMigrations.Version;

namespace UnitTests;

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public class MigrationTests
{
    [Test]
    [DisplayName("Initialize migration")]
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