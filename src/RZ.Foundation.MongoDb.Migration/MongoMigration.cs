using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using JetBrains.Annotations;
using LanguageExt.UnitsOfMeasure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDBMigrations;
using MongoDBMigrations.Document;
using RZ.Foundation.MongoDb.Migration.Helpers;
using Version = MongoDBMigrations.Version;

namespace RZ.Foundation.MongoDb.Migration;

[PublicAPI]
public static class MongoMigrationExtensions
{
    /// <summary>
    /// Standard Mongo connection string is resolved from the application's <c>appsettings.json</c> file, inside the section <c>ConnectionStrings</c>.
    /// If it's not set, then it'll be resolved from the environment variable <c>CS_CONNECTION</c> and <c>CS_DATABASE</c>.
    /// If they aren't found, it'll try to look for the environment variable <c>CS_CONFIGFILE</c> and read the connection string from the file.
    /// If none of the above is found, it'll throw an exception.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="connectionName"></param>
    /// <returns></returns>
    public static HostApplicationBuilder UseStandardMongoConnectionString(this HostApplicationBuilder builder, string connectionName = "MongoDb") {
        builder.Services.AddSingleton<IResolvedMongo>(sp => ActivatorUtilities.CreateInstance<StandardResolver>(sp, connectionName));
        return builder;
    }

    /// <summary>
    /// Start migration process and look for the version to migrate to from <param name="args">program arguments</param>.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="args">
    /// The first element of array should be a Semver, or either <c>latest</c> keyword or <c>downgrade</c> keyword.
    /// If it's an empty array, it's considered as <c>latest</c> keyword.
    /// </param>
    public static void RunAspireMigration(this HostApplicationBuilder builder, IEnumerable<string> args) {
        builder.Services.AddSingleton<IResolvedMongo, AspireStyleResolver>();

        builder.RunMigration(args);
    }

    /// <summary>
    /// Start migration process and look for the version to migrate to from <param name="args">program arguments</param>.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="args">
    /// The first element of array should be a Semver, or either <c>latest</c> keyword or <c>downgrade</c> keyword.
    /// If it's an empty array, it's considered as <c>latest</c> keyword.
    /// </param>
    public static void RunMigration(this HostApplicationBuilder builder, IEnumerable<string> args) {
        using var activity = MongoMigration.Source.StartActivity();

        activity?.AddTag("args", args);

        _ = new MigrationEngine(); // just to get effect of Mongo type registration

        builder.Services
               .AddSingleton(_ => new MongoMigration.ProgramArguments(args.FirstOrDefault()))
               .AddHostedService<MongoMigration>();
        builder.Build().Run();
    }
}

[PublicAPI, ExcludeFromCodeCoverage]
public partial class MongoMigration(ILogger<MongoMigration> logger, IConfiguration config, IServiceProvider sp,
                                    IResolvedMongo mongoSolution, MongoMigration.ProgramArguments args) : IHostedService
{
    public static readonly ActivitySource Source = new("RZ.Foundation.MongoDb.Migration", AppVersion.Current);

    public sealed record ProgramArguments(string? Version);

    const string DelayExitEnv = "DelayExit";
    const string UpgradeVersionEnv = "UpgradeVersion";

    Activity? activity;

    public static void Start(IEnumerable<string> args, string connectionName = "MongoDb") {
        using var activity = Source.StartActivity();

        activity?.AddTag("connection.name", connectionName);

        var builder = Host.CreateApplicationBuilder();
        builder.UseStandardMongoConnectionString(connectionName);
        builder.RunMigration(args);
    }

    [LoggerMessage(LogLevel.Information, "Database  : {DatabaseName}")]
    partial void LogDatabaseName(string DatabaseName);

    public Task StartAsync(CancellationToken cancellationToken) {
        activity = Source.StartActivity();

        var versionText = args.Version ?? config[UpgradeVersionEnv] ?? LatestKeyword;
        var version = ParseVersion(versionText) ?? ParseSpecialVersion(mongoSolution.Database, versionText);

        LogDatabaseName(mongoSolution.Database.DatabaseNamespace.DatabaseName);

        activity?.AddTag("version", version);

        if (version is null)
            logger.LogInformation("Update to date");
        else
            Migrate(version.Value);

        activity?.SetStatus(ActivityStatusCode.Ok);

        var lifetime = sp.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStarted.Register(() => {
            activity?.Stop();
            activity?.Dispose();

            logger.LogInformation("Migration completed. Exiting...");
            lifetime.StopApplication();
        });
        return Task.CompletedTask;
    }

    void Migrate(Version version) {
        logger.LogInformation("Migrating to version: {Version}", version.ToString());

        var migration = new MigrationEngine()
                        // It'd be better if we can pass IMongoDatabase
                       .UseDatabase(mongoSolution.Client, mongoSolution.Database.DatabaseNamespace.DatabaseName)
                       .UseAssembly(Assembly.GetEntryAssembly()!)
                       .UseSchemeValidation(false);

        migration.Run(version);

        var delay = Optional(config[DelayExitEnv]).Bind(s => int.TryParse(s, out var v) ? Some(v) : None);
        delay.Iter(d => {
            logger.LogInformation("Delay for {Delay} seconds...", d);
            Thread.Sleep(d.Seconds());
        });
        logger.LogInformation("End migration");
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    static Version? ParseVersion(string s) {
        var parts = s.Split('.');
        return parts.Length == 3? new(NumAt(0), NumAt(1), NumAt(2)) : null;

        int NumAt(int pos) => int.Parse(parts[pos]);
    }

    const string LatestKeyword = "latest";
    const string DowngradeKeyword = "downgrade";
    Version? ParseSpecialVersion(IMongoDatabase db, string version) {
        if (version is not LatestKeyword and not DowngradeKeyword)
            throw new ArgumentException($"Invalid version keyword. Only '{LatestKeyword}', '{DowngradeKeyword}', or Semver is accepted.", nameof(version));

        var locator = MigrationSource.FromAssembly(Assembly.GetEntryAssembly()!);
        var dbManager = new DatabaseManager(db, MongoEmulationEnum.None);
        var current = dbManager.GetVersion();

        logger.LogDebug("Current version: {Current}", current);

        var target = (from m in locator.Migrations
                      where version == LatestKeyword ? m.Version > current : m.Version < current
                      orderby m.Version descending
                      select m).FirstOrDefault();
        return target?.Version;
    }
}