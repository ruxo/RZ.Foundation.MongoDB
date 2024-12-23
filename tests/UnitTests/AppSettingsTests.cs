using FluentAssertions;
using RZ.Foundation;
using RZ.Foundation.MongoDb;
using RZ.Foundation.MongoDb.Migration;
using RZ.Foundation.Types;

namespace UnitTests;

public class AppSettingsTests
{
    [Fact(DisplayName = "Get ConnectionSettings from MongoConnectionString with database option")]
    public void UseDatabaseParameter() {
        var mcs = MongoConnectionString.From("mongodb://localhost:27017/?database=test");
        var cs = AppSettings.From(mcs!.Value);

        // then
        cs.Should().Be(new ConnectionSettings("mongodb://localhost:27017", "test"));
    }

    [Fact(DisplayName = "Use authorization source")]
    public void UseAuthorizationSource() {
        const string FullConnectionString = "mongodb+srv://user:password@mongo.net/dbname?retryWrites=true&w=majority&appName=AppTest";
        var mcs = MongoConnectionString.From(FullConnectionString);
        var cs = AppSettings.From(mcs!.Value);

        cs.Should().Be(new ConnectionSettings("mongodb+srv://user:password@mongo.net/dbname?appName=AppTest&retryWrites=true&w=majority", "dbname"));
    }

    [Fact(DisplayName = "Get ConnectionSettings from environment where nothing is set, will throw exception")]
    public void GetFromEnvironment() {
        var action = () => AppSettings.FromEnvironment(null);

        // then
        var error = action.Should().Throw<ErrorInfoException>();
        error.Which.Code.Should().Be(StandardErrorCodes.MissingConfiguration);
    }

    [Fact(DisplayName = "Get database from connection string")]
    public void GetDatabaseFromConnection() {
        const string FullConnectionString = "mongodb+srv://user:password@mongo.net/?retryWrites=true&w=majority&appName=AppTest&database=dbname";
        var mcs = MongoConnectionString.From(FullConnectionString);
        var cs = AppSettings.From(mcs!.Value);

        // then
        cs.Should().Be(new ConnectionSettings("mongodb+srv://user:password@mongo.net?appName=AppTest&retryWrites=true&w=majority", "dbname"));
    }
}
