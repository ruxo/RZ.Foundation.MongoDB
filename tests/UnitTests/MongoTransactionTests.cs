using FluentAssertions;
using MongoDB.Driver;
using RZ.Foundation.MongoDb;
using static UnitTests.TestSample;

namespace UnitTests;

public sealed class MongoTransactionTests
{
    [Fact(DisplayName = "Add people with transaction")]
    public async Task AddPeopleWithTransaction() {
        var mdb = MockDb.StartDb();

        // when
        await using var transaction = mdb.Db.CreateTransaction();
        await transaction.GetCollection<Customer>().Add(JohnDoe, cancel: TestContext.Current.CancellationToken);
        await transaction.GetCollection<Customer>().Add(JaneDoe, cancel: TestContext.Current.CancellationToken);
        await transaction.Commit();

        // then
        var people = await mdb.Db.GetCollection<Customer>().Find(_ => true).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        people.Should().BeEquivalentTo([ JohnDoe, JaneDoe ]);
    }

    [Fact(DisplayName = "Auto rollback if no explicit commit")]
    public async Task AutoRollbackIfNoExplicitCommit() {
        var mdb = MockDb.StartDb();

        // when
        await AddCustomers();

        // then
        var people = await mdb.Db.GetCollection<Customer>().Find(_ => true).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        people.Count.Should().Be(0);
        return;

        async Task AddCustomers() {
            await using var transaction = mdb.Db.CreateTransaction();
            await transaction.GetCollection<Customer>().Add(JohnDoe);
            await transaction.GetCollection<Customer>().Add(JaneDoe);
        }
    }
}