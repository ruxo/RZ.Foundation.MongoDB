using System.Text.Json;
using RZ.Foundation.Types;

namespace RZ.Foundation.MongoDb.Migration;

public record struct ConnectionSettings(string ConnectionString, string DatabaseName);

public static class AppSettings
{
    const string EnvConnectionString = "CS_CONNECTION";
    const string EnvDatabaseName = "CS_DATABASE";
    const string EnvFileConfig = "CS_CONFIGFILE";

    public static Outcome<ConnectionSettings> FromEnvironment(string? connectionString) {
        if (Fail(connectionString ?? GetEnv(EnvConnectionString), out var e, out var connection) && e.Code != StandardErrorCodes.NotFound) return e;
        if (Fail(GetEnv(EnvDatabaseName), out e, out var dbName) && e.Code != StandardErrorCodes.NotFound) return e;

        ConnectionSettings? settings = connection is not null && dbName is not null? new ConnectionSettings(connection, dbName) : null;
        if (Success(settings ?? GetEnv(EnvFileConfig).Bind(GetFromFile), out var final, out e)){
            Console.WriteLine($"Configured Connection: [{connection}]");
            return final;
        }
        return new ErrorInfo(StandardErrorCodes.MissingConfiguration, GetErrorMessage(), innerError: e);

        string GetErrorMessage()
            => connectionString is null
                   ? $"No connection settings in {EnvConnectionString}, {EnvDatabaseName}, or {EnvFileConfig}"
                   : $"No database name in {EnvDatabaseName} or {EnvFileConfig}";
    }

    public static ConnectionSettings? From(MongoConnectionString connectionString) {
        var (connection, dbName) = connectionString;
        return dbName?.Apply(databaseName => new ConnectionSettings(connection.ToString(), databaseName));
    }

    static Outcome<ConnectionSettings> GetFromFile(string filename) {
        try{
            return JsonSerializer.Deserialize<ConnectionSettings>(File.ReadAllText(filename));
        }
        catch (Exception e){
            return ErrorFrom.Exception(e);
        }
    }

    static Outcome<string> GetEnv(string key) {
        try{
            return Environment.GetEnvironmentVariable(key) is { } value ? value : new ErrorInfo(StandardErrorCodes.NotFound);
        }
        catch (Exception e){
            return ErrorFrom.Exception(e);
        }
    }
}