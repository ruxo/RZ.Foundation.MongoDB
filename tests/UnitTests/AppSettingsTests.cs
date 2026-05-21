using JetBrains.Annotations;
using RZ.Foundation;
using RZ.Foundation.MongoDb;
using RZ.Foundation.MongoDb.Migration;
using RZ.Foundation.Types;

namespace UnitTests;

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public class AppSettingsTests
{
    [Test]
    [DisplayName("Get ConnectionSettings from MongoConnectionString with database option")]
    public async ValueTask UseDatabaseParameter() {
        var mcs = MongoConnectionString.From("mongodb://localhost:27017/?database=test");
        var cs = AppSettings.From(mcs!.Value);

        // then
        await Assert.That(cs).IsEquivalentTo(new ConnectionSettings("mongodb://localhost:27017", "test"));
    }

    [Test]
    [DisplayName("Use authorization source")]
    public async ValueTask UseAuthorizationSource() {
        const string FullConnectionString = "mongodb+srv://user:password@mongo.net/dbname?retryWrites=true&w=majority&appName=AppTest";
        var mcs = MongoConnectionString.From(FullConnectionString);
        var cs = AppSettings.From(mcs!.Value);

        await Assert.That(cs).IsEquivalentTo(new ConnectionSettings("mongodb+srv://user:password@mongo.net/dbname?appName=AppTest&retryWrites=true&w=majority", "dbname"));
    }

    [Test]
    [DisplayName("Get ConnectionSettings from environment where nothing is set, will throw exception")]
    public async ValueTask GetFromEnvironment() {
        var result = AppSettings.FromEnvironment(null);

        // then
        await Assert.That(result.IsFail).IsTrue();
        await Assert.That(result.UnwrapError().Code).IsEqualTo(StandardErrorCodes.MissingConfiguration);
    }

    [Test]
    [DisplayName("Get database from connection string")]
    public async ValueTask GetDatabaseFromConnection() {
        const string FullConnectionString = "mongodb+srv://user:password@mongo.net/?retryWrites=true&w=majority&appName=AppTest&database=dbname";
        var mcs = MongoConnectionString.From(FullConnectionString);
        var cs = AppSettings.From(mcs!.Value);

        // then
        await Assert.That(cs).IsEquivalentTo(new ConnectionSettings("mongodb+srv://user:password@mongo.net?appName=AppTest&retryWrites=true&w=majority", "dbname"));
    }
}
