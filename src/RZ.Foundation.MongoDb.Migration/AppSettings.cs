using System.Text.Json;
using RZ.Foundation.Types;

namespace RZ.Foundation.MongoDb.Migration;

public record struct ConnectionSettings(string ConnectionString, string DatabaseName);

public static class AppSettings
{
    const string EnvConnectionString = "CS_CONNECTION";
    const string EnvDatabaseName = "CS_DATABASE";
    const string EnvFileConfig = "CS_CONFIGFILE";

    public static ConnectionSettings FromEnvironment(string? connectionString) {
        var connection = connectionString ?? GetEnv(EnvConnectionString);
        var dbName = GetEnv(EnvDatabaseName);

        ConnectionSettings? settings = connection is not null && dbName is not null? new ConnectionSettings(connection, dbName) : null;
        var final = settings ?? GetEnv(EnvFileConfig)?.Apply(GetFromFile);

        if (final is null)
            Console.WriteLine($"Configured Connection: [{connection}]");

        return final ?? throw new ErrorInfoException(StandardErrorCodes.MissingConfiguration, GetErrorMessage());

        string GetErrorMessage()
            => connectionString is null
                   ? $"No connection settings in {EnvConnectionString}, {EnvDatabaseName}, or {EnvFileConfig}"
                   : $"No database name in {EnvDatabaseName} or {EnvFileConfig}";
    }

    public static ConnectionSettings? From(MongoConnectionString connectionString) {
        var (connection, dbName) = connectionString;
        return dbName.ApplyValue(databaseName => new ConnectionSettings(connection.ToString(), databaseName));
    }

    static ConnectionSettings GetFromFile(string filename) =>
        JsonSerializer.Deserialize<ConnectionSettings>(File.ReadAllText(filename));

    static string? GetEnv(string key)
        => Environment.GetEnvironmentVariable(key);
}