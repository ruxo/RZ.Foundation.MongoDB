using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using JetBrains.Annotations;
using LanguageExt.UnitsOfMeasure;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using MongoDBMigrations;
using MongoDBMigrations.Document;
using RZ.AspNet;
using Version = MongoDBMigrations.Version;

namespace RZ.Foundation.MongoDb.Migration;

[PublicAPI, ExcludeFromCodeCoverage]
public static class MongoMigration
{
    const string DelayExitEnv = "DelayExit";
    const string UpgradeVersionEnv = "UpgradeVersion";

    public static void Start(IEnumerable<string> args, string connectionName = "MongoDb") {
        _ = new MigrationEngine(); // just to get effect of Mongo type registration

        var config = AspHost.CreateDefaultConfigurationSettings();
        var versionText = args.FirstOrDefault() ?? config[UpgradeVersionEnv] ?? LatestKeyword;

        var cs = config.GetConnectionString(connectionName) ?? throw new ArgumentException("Invalid connection string name", connectionName);
        var mcs = MongoConnectionString.From(cs) ?? throw new ArgumentException("Invalid Mongo connection string", cs);
        var connectionSettings = AppSettings.From(mcs) ?? AppSettings.FromEnvironment(mcs.ToString());
        var version = ParseVersion(versionText) ?? ParseSpecialVersion(connectionSettings, versionText);

        Console.WriteLine("Database  : {0}", connectionSettings.DatabaseName);

        if (version is null){
            Console.WriteLine("Update to date.");
            return;
        }
        Console.WriteLine("Migrating to version: {0}", version.ToString());

        var client = new MongoClient(connectionSettings.ConnectionString);

        var migration = new MigrationEngine()
                       .UseDatabase(client, connectionSettings.DatabaseName)
                       .UseAssembly(Assembly.GetEntryAssembly()!)
                       .UseSchemeValidation(false);

        migration.Run(version);

        var delay = Optional(config[DelayExitEnv]).Bind(s => int.TryParse(s, out var v) ? Some(v) : None);
        delay.Iter(d => {
            Console.WriteLine("Delay for {0} seconds...", d);
            Thread.Sleep(d.Seconds());
        });
        Console.WriteLine("End migration.");
    }

    static Version? ParseVersion(string s) {
        var parts = s.Split('.');
        return parts.Length == 3? new(NumAt(0), NumAt(1), NumAt(2)) : null;

        int NumAt(int pos) => int.Parse(parts[pos]);
    }

    const string LatestKeyword = "latest";
    const string DowngradeKeyword = "downgrade";
    static Version? ParseSpecialVersion(ConnectionSettings settings, string version) {
        if (version is not LatestKeyword and not DowngradeKeyword)
            throw new ArgumentException($"Invalid version keyword. Only '{LatestKeyword}', '{DowngradeKeyword}', or Semver is accepted.", nameof(version));

        var db = new MongoClient(settings.ConnectionString).GetDatabase(settings.DatabaseName);
        var locator = MigrationSource.FromAssembly(Assembly.GetEntryAssembly()!);
        var dbManager = new DatabaseManager(db, MongoEmulationEnum.None);
        var current = dbManager.GetVersion();
        var target = (from m in locator.Migrations
                      where version == LatestKeyword ? m.Version > current : m.Version < current
                      orderby m.Version descending
                      select m).FirstOrDefault();
        return target?.Version;
    }
}