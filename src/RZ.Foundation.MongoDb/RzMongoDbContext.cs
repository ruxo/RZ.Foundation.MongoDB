using JetBrains.Annotations;
using MongoDB.Driver;
using RZ.Foundation.Types;
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace RZ.Foundation.MongoDb;

[PublicAPI]
public interface IRzMongoDatabase
{
    IMongoCollection<T> GetCollection<T>();
}

[PublicAPI]
public interface IRzMongoDbContext : IRzMongoDatabase
{
    IRzMongoTransaction CreateTransaction();
}

[PublicAPI]
public abstract class RzAspireMongoDbContext : IRzMongoDbContext
{
    protected IMongoClient Client = null!;
    protected IMongoDatabase Db = null!;

    protected RzAspireMongoDbContext(IMongoClient client, IMongoDatabase db) {
        Client = client;
        Db = db;
    }

    protected RzAspireMongoDbContext() { }

    public IMongoCollection<T> GetCollection<T>()
        => Db.GetCollection<T>(MongoHelper.GetCollectionName<T>());

    public IRzMongoTransaction CreateTransaction() {
        var session = Client.StartSession();
        session.StartTransaction();
        return new RzMongoTransaction(Guid.NewGuid(), Db, session);
    }
}

[PublicAPI]
public abstract class RzMongoDbContext : IRzMongoDbContext
{
    readonly Lazy<IMongoClient> client;
    readonly Lazy<IMongoDatabase> db;

    protected RzMongoDbContext(string connectionString)
        : this(MongoConnectionString.From(connectionString) ??
               throw new ErrorInfoException(StandardErrorCodes.InvalidRequest, "Database connection string is invalid", debugInfo: connectionString)) { }

    protected RzMongoDbContext(MongoConnectionString connection)
        : this(connection.GetValidConnectionString(),
               connection.DatabaseName ?? throw new ErrorInfoException(StandardErrorCodes.InvalidRequest, "Database name is missing")) { }

    protected RzMongoDbContext(MongoConnectionString connection, string dbName)
    {
        client = new(() => new MongoClient(connection.ToString()));
        db = new(() => client.Value.GetDatabase(dbName));
    }

    public IMongoCollection<T> GetCollection<T>()
        => db.Value.GetCollection<T>(MongoHelper.GetCollectionName<T>());

    public IRzMongoTransaction CreateTransaction() {
        var session = client.Value.StartSession();
        session.StartTransaction();
        return new RzMongoTransaction(Guid.NewGuid(), db.Value, session);
    }
}