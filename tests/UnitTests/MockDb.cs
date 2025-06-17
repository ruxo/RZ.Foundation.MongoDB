using System.Diagnostics;
using JetBrains.Annotations;
using MongoSandbox;
using RZ.Foundation.MongoDb;

namespace UnitTests;

public sealed class TestDbContext(MongoConnectionString connection, string dbName) : RzMongoDbContext(connection, dbName);

public sealed record MockedDatabase(string ConnectionString, TestDbContext Db);

[PublicAPI]
public static class MockDb
{
    static readonly MongoRunnerOptions MongoOptions = new() {
        UseSingleNodeReplicaSet = true,
        StandardOutputLogger = s => Trace.WriteLine($"| {s}"),
        StandardErrorLogger = s => Trace.WriteLine($"ERR: {s}")
    };
    static readonly IMongoRunner Server = StartServer();

    static IMongoRunner StartServer() {
        MongoHelper.SetupMongoStandardMappings();
        return MongoRunner.Run(MongoOptions);
    }

    static int dbCount;

    public static MockedDatabase StartDb()
        => new(Server.ConnectionString,
               new TestDbContext(
                   MongoConnectionString.From(Server.ConnectionString)!.Value,
                   $"TestDb{Interlocked.Increment(ref dbCount)}"));

    public static MockedDatabase StartWithSample() {
        var x = StartDb();
        x.Db.GetCollection<Customer>().ImportSamples();
        return x;
    }
}