using MongoDB.Driver;
using RZ.Foundation.MongoDb;
using static UnitTests.TestSample;

namespace UnitTests;

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public sealed class MongoTransactionTests
{
    [Test]
    [DisplayName("Add people with transaction")]
    public async Task AddPeopleWithTransaction(CancellationToken cancel) {
        var mdb = MockDb.StartDb();

        // when
        await using var transaction = mdb.Db.CreateTransaction();
        await transaction.GetCollection<Customer>().Add(JohnDoe, cancel);
        await transaction.GetCollection<Customer>().Add(JaneDoe, cancel);
        await transaction.Commit();

        // then
        var people = await mdb.Db.GetCollection<Customer>().Find(_ => true).ToListAsync(cancel);
        await Assert.That(people).IsEquivalentTo([ JohnDoe, JaneDoe ]);
    }

    [Test]
    [DisplayName("Auto rollback if no explicit commit")]
    public async Task AutoRollbackIfNoExplicitCommit(CancellationToken cancel) {
        var mdb = MockDb.StartDb();

        // when
        await AddCustomers();

        // then
        var people = await mdb.Db.GetCollection<Customer>().Find(_ => true).ToListAsync(cancel);
        await Assert.That(people.Count).IsEqualTo(0);
        return;

        async Task AddCustomers() {
            await using var transaction = mdb.Db.CreateTransaction();
            await transaction.GetCollection<Customer>().Add(JohnDoe);
            await transaction.GetCollection<Customer>().Add(JaneDoe);
        }
    }
}