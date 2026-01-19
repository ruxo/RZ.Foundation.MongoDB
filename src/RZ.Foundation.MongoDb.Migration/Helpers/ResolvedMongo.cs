using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace RZ.Foundation.MongoDb.Migration.Helpers;

public interface IResolvedMongo{
    IMongoClient Client { get; }
    IMongoDatabase Database { get; }
}

public class StandardResolver : IResolvedMongo
{
    public StandardResolver(ILogger<StandardResolver> logger, IConfiguration config, string connectionName) {
        logger.LogInformation("Resolving connection string for {ConnectionName}", connectionName);

        var cs = config.GetConnectionString(connectionName) ?? throw new ArgumentException("Invalid connection string name", connectionName);
        var mcs = MongoConnectionString.From(cs) ?? throw new ArgumentException("Invalid Mongo connection string", cs);
        var connectionSettings = AppSettings.From(mcs) ?? AppSettings.FromEnvironment(mcs.ToString());

        Client = new MongoClient(mcs.ToString());
        Database = Client.GetDatabase(connectionSettings.Unwrap().DatabaseName);
    }

    public IMongoClient Client { get; }
    public IMongoDatabase Database { get; }
}

public class AspireStyleResolver(IMongoClient client, IMongoDatabase database) : IResolvedMongo
{
    public IMongoClient Client => client;
    public IMongoDatabase Database => database;
}