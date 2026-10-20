using MongoDB.Driver;
using RZ.Foundation;
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

    [Test]
    [DisplayName("Two transactions write the same document, the second write is a race condition")]
    public async Task TwoTransactionsWriteTheSameDocument(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();
        await using var first = mdb.Db.CreateTransaction();
        await using var second = mdb.Db.CreateTransaction();

        // when
        var firstWrite = await first.GetCollection<Customer>().Update(JaneDoe.Id, JaneWithZip("22222"), cancel: cancel);
        var secondWrite = await second.GetCollection<Customer>().Update(JaneDoe.Id, JaneWithZip("33333"), cancel: cancel);

        // then
        await Assert.That(firstWrite.IsSuccess).IsTrue();
        await Assert.That(secondWrite.IfFail(out var error, out _)).IsTrue();
        await Assert.That(error!.Code).IsEqualTo(StandardErrorCodes.RaceCondition);

        await ThrowIfError(first.Commit());
        var jane = await ThrowIfError(mdb.Db.GetCollection<Customer>().GetById(JaneDoe.Id, cancel));
        await Assert.That(jane.Address.Zip).IsEqualTo("22222").Because("the first transaction's write stands");
    }

    [Test]
    [DisplayName("A transaction writes a document changed outside it after it began, the write is a race condition")]
    public async Task TransactionWritesDocumentChangedOutsideIt(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();
        var customers = mdb.Db.GetCollection<Customer>();
        await using var transaction = mdb.Db.CreateTransaction();
        await ThrowIfError(transaction.GetCollection<Customer>().GetById(JaneDoe.Id, cancel));

        // when
        var outsideWrite = await customers.Update(JaneDoe.Id, JaneWithZip("22222"), cancel: cancel);
        var transactionWrite = await transaction.GetCollection<Customer>().Update(JaneDoe.Id, JaneWithZip("33333"), cancel: cancel);

        // then
        await Assert.That(outsideWrite.IsSuccess).IsTrue();
        await Assert.That(transactionWrite.IfFail(out var error, out _)).IsTrue();
        await Assert.That(error!.Code).IsEqualTo(StandardErrorCodes.RaceCondition);

        var jane = await ThrowIfError(customers.GetById(JaneDoe.Id, cancel));
        await Assert.That(jane.Address.Zip).IsEqualTo("22222");
    }

    [Test]
    [DisplayName("A write outside any transaction waits for the transaction holding the document, then succeeds")]
    public async Task WriteOutsideAnyTransactionIsUnaffected(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();
        var customers = mdb.Db.GetCollection<Customer>();
        await using var transaction = mdb.Db.CreateTransaction();
        await ThrowIfError(transaction.GetCollection<Customer>().Update(JaneDoe.Id, JaneWithZip("22222"), cancel: cancel));

        // when
        var outsideWrite = customers.Update(JaneDoe.Id, JaneWithZip("33333"), cancel: cancel);
        await ThrowIfError(transaction.Commit());
        var result = await outsideWrite;

        // then
        await Assert.That(result.IsSuccess).IsTrue();
        var jane = await ThrowIfError(customers.GetById(JaneDoe.Id, cancel));
        await Assert.That(jane.Address.Zip).IsEqualTo("33333");
    }

    static Customer JaneWithZip(string zip)
        => JaneDoe with { Address = JaneDoe.Address with { Zip = zip } };
}