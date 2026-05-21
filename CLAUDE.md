# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository layout

Solution file is `RZ.Foundation.MongoDb.slnx` (the new XML solution format — open with `dotnet sln` or recent IDEs only; legacy `.sln` is **not** present).

- `src/RZ.Foundation.MongoDb/` — core library: connection-string parsing, DB context, transaction wrapper, MongoDB CRUD extension methods returning `Outcome<T>`.
- `src/RZ.Foundation.MongoDb.Migration/` — migration runner built on `MongoDBMigrationsRZ` + `Microsoft.Extensions.Hosting`; resolves connection from `appsettings.json` / env vars and runs an `IHostedService`.
- `tests/UnitTests/` — TUnit test project; uses `MongoSandbox` (in-process MongoDB) so tests need no external Mongo. Windows x64 only via `MongoSandbox8.runtime.win-x64`.
- `src/Directory.Build.props` injects `MinVer` (git-tag-based versioning) and `src/CommonGlobalUsings.cs` (`LanguageExt`, `RZ.Foundation.Prelude`, `RZ.Foundation.AOT.Prelude`, `RZ.Foundation.Extensions`) into both src projects.
- `Directory.Packages.props` is the single source of truth for package versions (`ManagePackageVersionsCentrally=true`); add new packages with `<PackageVersion>` here and `<PackageReference>` (no version) in the `.csproj`.

## Common commands

```powershell
dotnet restore                                                          # restore (uses central versions)
dotnet build                                                            # build all
dotnet test                                                             # run all tests (TUnit / Microsoft.Testing.Platform)
dotnet test --filter "DisplayName~Add people with transaction"          # run one test by DisplayName
dotnet test --filter "FullyQualifiedName~UnitTests.Update"              # run a single test class
.\build.ps1 <output-dir>                                                # pack both nupkgs into <output-dir>
```

Target framework is `net10.0` with `LangVersion=preview` (the library uses C# `extension<T>(...)` blocks — these require a preview-capable SDK; see `global.json`). Tests use TUnit's Microsoft.Testing.Platform runner (configured in `global.json`).

## Architecture

### Outcome-based result paradigm
The whole MongoDB surface area returns `Outcome<T>` (from `RZ.Foundation`) instead of throwing — this is the project's defining choice (see `CHANGES.md` 8.0.0). The extension methods in `MongoClientExtensions.cs` (`Add`, `Update`, `Upsert`, `Delete`, `Get`, `GetById`, `ExecuteList`, `GetCursor`, `Enumerate`, …) all wrap MongoDB driver calls in try/catch and funnel exceptions through `MongoHelper.InterpretDatabaseError` so callers handle `ErrorInfo` (with codes like `Duplication`, `RaceCondition`, `DatabaseTransactionError`) rather than exceptions. When adding new operations, follow the same pattern — never let raw `MongoException`s escape.

### Optimistic concurrency via `IHaveVersion` / `ICanUpdateVersion<T>`
Domain types may implement `IHaveKey<TKey>` (required for keyed Update/Delete) and optionally `ICanUpdateVersion<T>` (defines `WithVersion(updated, next)`). When a type implements `ICanUpdateVersion`, `Update`/`Upsert` automatically:
1. filter on `Id` AND current `Version`,
2. bump `Updated` (from injected `TimeProvider`) and `Version`,
3. return `StandardErrorCodes.RaceCondition` if the matched/modified count is 0 (i.e., someone else updated first).
This is implemented in `MongoClientExtensions.GetUpdateCondition` / `InterpretReplaceResult`. `Customer` in `tests/UnitTests/TestSample.cs` is the canonical example.

### Collection naming
`CollectionNameAttribute` overrides the default (type name). `MongoHelper.GetCollectionName<T>()` caches the result. Use it — don't hard-code collection names.

### Transactions
`IRzMongoDbContext.CreateTransaction()` returns an `IRzMongoTransaction` whose `GetCollection<T>()` returns a session-bound `IMongoCollection<T>` wrapper (`RzMongoTransaction.Wrapper<T>` in `RzMongoTransaction.cs`). The wrapper forwards every call to the session-aware driver overload; a few synchronous methods like `Count(...)` throw `NotSupportedException` on purpose. `DisposeAsync` auto-rolls back if `Commit()` was not called.

### Connection string handling
`MongoConnectionString` (`MongoConnectionString.cs`) is a parsed record. It supports a **non-standard `database=` query option** as an explicit DB name; `GetValidConnectionString()` strips it before handing the string to the driver. Database name resolution priority: `database` option → path segment (auth DB) → `authSource` option. Tests in `AppSettingsTests.cs` document the expected behavior.

### Migration project
Two ways to wire up: `HostApplicationBuilder.UseStandardMongoConnectionString()` (reads `ConnectionStrings:<name>` from configuration with env-var fallback `CS_CONNECTION` / `CS_DATABASE` / `CS_CONFIGFILE`) or `RunAspireMigration` (relies on already-DI-registered `IMongoClient` + `IMongoDatabase`). `RunMigration(args)` registers a hosted service that reads the target version from `args[0]` (semver, `latest`, or `downgrade`), uses `MongoDBMigrations.MigrationEngine` to scan `Assembly.GetEntryAssembly()` for `IMigration` implementations, and shuts the host down when done. `Migration.Build<T>(db).WithSchema(...).UniqueIndex(...).Run(session)` is the fluent helper for writing those `IMigration.Up` methods (see `tests/UnitTests/Migration/MigrationTestScript.cs`).

### Bootstrap
Apps must call `MongoHelper.SetupMongoStandardMappings()` once at startup. It registers `DateTimeOffset` → BSON DateTime, GUID standard representation, and an `EnumRepresentationConvention(BsonType.String)` convention pack. Tests do this inside `MockDb.StartServer()`.

## Tests

- `MockDb.StartDb()` boots a single in-process MongoDB replica set via `MongoSandbox` (one server per test process, fresh DB per call). `StartWithSample()` also seeds three `Customer` rows from `TestSample.cs`.
- Tests use TUnit attributes (`[Test]`, `[DisplayName]`, `await Assert.That(...)`). Pattern: arrange → `// when` → `// then`.
- The runner is Microsoft.Testing.Platform (set in `global.json`). Don't add xUnit/NUnit packages.
