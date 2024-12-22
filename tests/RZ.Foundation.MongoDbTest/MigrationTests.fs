namespace MongoDbTest

open System
open System.Reflection
open FluentAssertions
open MongoDB.Driver
open MongoDBMigrations
open RZ.Foundation
open RZ.Foundation.MongoDb
open RZ.Foundation.MongoDb.Migration
open RZ.Foundation.MongoDbTest
open RZ.Foundation.Types
open Xunit
open MockDb

type ``Migration tests``(output) =
    [<Fact>]
    let ``Initialize migration`` () =
        use mdb = startTransactDb output

        let client = MongoClient(mdb.ConnectionString)
        let migration = MigrationEngine()
                            .UseDatabase(client, "test")
                            .UseAssembly(Assembly.GetExecutingAssembly())
                            .UseSchemeValidation(enabled = false)

        migration.Run() |> ignore

        migration.Run(Version(0,0,1)) |> ignore
