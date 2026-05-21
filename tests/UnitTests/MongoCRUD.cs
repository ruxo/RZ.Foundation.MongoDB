using MongoDB.Driver;
using RZ.Foundation;
using RZ.Foundation.MongoDb;
using static UnitTests.TestSample;

namespace UnitTests;

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public class Add
{
    [Test]
    [DisplayName("Add single row and query")]
    public async Task AddSingleRowAndQuery(CancellationToken cancel) {
        var person = new Customer("John Doe", new("TH", "10000"), 0, new(2024, 1, 31, 17, 0, 0, TimeSpan.Zero), JohnDoe.Id);

        // when
        var mdb = MockDb.StartDb();
        await mdb.Db.GetCollection<Customer>().Add(person, cancel);

        // then
        var result = await mdb.Db.GetCollection<Customer>().GetById(person.Id, cancel);
        await Assert.That(result.Unwrap()).IsEquivalentTo(person);
    }

    [Test]
    [DisplayName("Repeatedly add the same single row will throw")]
    public async Task RepeatedlyAddTheSameSingleRowWillThrow(CancellationToken cancel) {
        var person = new Customer("John Doe", new Address("TH", "10000"), 0, new DateTimeOffset(2024, 1, 31, 17, 0, 0, TimeSpan.Zero), JohnDoe.Id);

        // when
        var mdb = MockDb.StartDb();
        var coll = mdb.Db.GetCollection<Customer>();
        await coll.Add(person, cancel);

        // then when inserting the same record the second time
        var result = await coll.Add(person, cancel);

        await Assert.That(result.IsFail).IsTrue();
        await Assert.That(result.UnwrapError().Code).IsEqualTo(StandardErrorCodes.Duplication);
    }

    [Test]
    [DisplayName("Capture duplicated add error with TryAdd")]
    public async Task CaptureDuplicatedAddErrorWithTryAdd(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();

        // when
        var result = await mdb.Db.GetCollection<Customer>().Add(new Customer("Example Name", new("TH", "10000"), 0, new(2020, 1, 1, 17, 0, 0, TimeSpan.Zero), new("711CA94D-239C-4E67-81C9-1F2F155B3F43")), cancel);

        // then
        await Assert.That(result.IfFail(out var error, out _)).IsTrue();
        await Assert.That(error!.Code).IsEqualTo(StandardErrorCodes.Duplication);
    }

    [Test]
    [DisplayName("Simple add with TryAdd")]
    public async Task SimpleAddWithTryAdd(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();

        // when
        var result = await mdb.Db.GetCollection<Customer>().Add(new("Testla Namera", new("XY", "10000"), 0, new(2020, 1, 1, 17, 0, 0, TimeSpan.Zero), UnusedGuid1), cancel);

        // then
        await Assert.That(result.IsSuccess).IsTrue();
    }
}

public class Retrieval
{
    [Test]
    [DisplayName("Get the first customer with zip code 11111")]
    public async Task GetFirstCustomerWithZipCode11111(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();

        // when
        var result = await mdb.Db.GetCollection<Customer>().Get(x => x.Address.Zip == "11111", cancel);

        // then
        await Assert.That(result.Unwrap()).IsEquivalentTo(
            new Customer("John Doe",
                         new Address("TH", "11111"),
                         1,
                         new DateTimeOffset(2020, 1, 1, 17, 0, 0, TimeSpan.Zero),
                         new Guid("0B8D9631-720A-46B7-8C95-F55B4EC520A4")
                ));
    }

    [Test]
    [DisplayName("Get all customers with country 'TH'")]
    public async Task GetAllCustomersWithCountryTh(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();

        // when
        var result = await ThrowIfError(mdb.Db.GetCollection<Customer>()
                                           .FindAsync(x => x.Address.Country == "TH", cancellationToken: cancel)
                                           .Retrieve(x => x.ExecuteList()));

        // then
        await Assert.That(result.Count).IsEqualTo(2);

        var names = result.Select(x => x.Name);
        await Assert.That(names).IsEquivalentTo([JohnDoe.Name, JaneDoe.Name]);
    }
}

public class Update
{
    [Test]
    [DisplayName("Update Jane's zip code")]
    public async Task UpdateJaneZipCode(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        TimeProvider time = new FixedClock(NewYear2024);

        // when
        var jane = await ThrowIfError(customer.GetById(JaneDoe.Id, cancel: cancel));
        var updatedJane = jane with { Address = jane.Address with { Zip = "22222" } };
        await customer.Update<Customer, Guid>(updatedJane, clock: time, cancel: cancel);

        // then
        var expected = updatedJane with { Updated = NewYear2024, Version = 3u };
        jane = await ThrowIfError(customer.GetById(JaneDoe.Id, cancel: cancel));
        await Assert.That(jane).IsEquivalentTo(expected);
    }

    [Test]
    [DisplayName("Try updating Jane Zip code must succeed")]
    public async Task TryUpdatingJaneZipCodeMustSucceed(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        TimeProvider time = new FixedClock(NewYear2024);

        // when
        var jane = await ThrowIfError(customer.GetById(JaneDoe.Id, cancel: cancel));
        var updatedJane = jane with { Address = jane.Address with { Zip = "22222" } };
        var result = await customer.Update<Customer, Guid>(updatedJane, clock: time, cancel: cancel);

        // then
        await Assert.That(result.IsSuccess).IsTrue();

        var expected = updatedJane with { Updated = NewYear2024, Version = 3u };
        jane = await ThrowIfError(customer.GetById(JaneDoe.Id, cancel: cancel));
        await Assert.That(jane).IsEquivalentTo(expected);
    }

    [Test]
    [DisplayName("Update Jane Zip code with explicit version number")]
    public async Task UpdateJaneZipCodeWithExplicitVersionNumber(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        // when
        var updatedJane = JaneDoe with { Address = JaneDoe.Address with { Zip = "22222" } };
        await customer.Update(JaneDoe.Id, updatedJane, JaneDoe.Version, cancel: cancel);

        // then
        var jane = await customer.GetById(JaneDoe.Id, cancel: cancel);
        await Assert.That(jane.Unwrap()).IsEquivalentTo(updatedJane);
    }

    [Test]
    [DisplayName("Update Jane Zip code with outdated explicit version number, results in race condition")]
    public async Task UpdateJaneZipCodeWithOutdatedExplicitVersionNumberResultsInRaceCondition(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        // when
        var updatedJane = JaneDoe with { Address = JaneDoe.Address with { Zip = "22222" } };
        var result = await customer.Update(JaneDoe.Id, updatedJane, 123u, cancel: cancel);

        // then
        await Assert.That(result.IsFail).IsTrue();
        var error = result.UnwrapError();

        await Assert.That(error.Code).IsEqualTo(StandardErrorCodes.RaceCondition);
    }

    [Test]
    [DisplayName("Try updating Jane Zip code with outdated explicit version number, results in race condition")]
    public async Task TryUpdatingJaneZipCodeWithOutdatedExplicitVersionNumberResultsInRaceCondition(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        var updatedJane = JaneDoe with { Address = JaneDoe.Address with { Zip = "22222" } };
        var result = await customer.Update(JaneDoe.Id, updatedJane, 123u, cancel: cancel);

        await Assert.That(result.IfFail(out var error, out _)).IsTrue();
        await Assert.That(error!.Code).IsEqualTo(StandardErrorCodes.RaceCondition);
    }

    [Test]
    [DisplayName("Update Jane Zip code with the explicit (new) key and data's key mismatch, results in race condition error")]
    public async Task UpdateJaneZipCodeWithExplicitNewKeyAndDataKeyMismatchResultsInRaceConditionError(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        var updatedJane = JaneDoe with { Address = JaneDoe.Address with { Zip = "22222" } };
        var result = await customer.Update(UnusedGuid1, updatedJane, cancel: cancel);

        await Assert.That(result.IsFail).IsTrue();
        await Assert.That(result.UnwrapError().Code).IsEqualTo(StandardErrorCodes.RaceCondition);
    }

    [Test]
    [DisplayName("Update Jane Zip code with the explicit (valid) key and data's key mismatch, results in database transaction error")]
    public async Task UpdateJaneZipCodeWithExplicitValidKeyAndDataKeyMismatchResultsInDatabaseTransactionError(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        var updatedJane = JaneDoe with { Address = JaneDoe.Address with { Zip = "22222" } };

        var result = await customer.Update(JohnDoe.Id, updatedJane, cancel: cancel);
        await Assert.That(result.UnwrapError().Code).IsEqualTo(StandardErrorCodes.DatabaseTransactionError);
    }

    [Test]
    [DisplayName("Update John zip code with his *unique* zip")]
    public async Task UpdateJohnZipCodeWithHisUniqueZip(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        var updatedJohn = JohnDoe with { Address = JohnDoe.Address with { Zip = "22222" } };
        await customer.Update(updatedJohn, x => x.Address.Zip == "11111", cancel: cancel);

        var john = await customer.GetById(JohnDoe.Id, cancel: cancel);
        await Assert.That(john.Unwrap()).IsEquivalentTo(updatedJohn);
    }

    [Test]
    [DisplayName("Update with multiple matches will result in ID overwritten which will fail")]
    public async Task UpdateWithMultipleMatchesWillResultInIDOverwrittenWhichWillFail(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        var result = await customer.Update(NewKid, x => x.Address.Country == "TH", cancel: cancel);
        await Assert.That(result.UnwrapError().Code).IsEqualTo(StandardErrorCodes.DatabaseTransactionError).Because("someone's ID was overwritten");
    }

    [Test]
    [DisplayName("Try updating with multiple matches will result in ID overwritten which will fail")]
    public async Task TryUpdatingWithMultipleMatchesWillResultInIDOverwrittenWhichWillFail(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        var result = await customer.Update(NewKid, x => x.Address.Country == "TH", cancel: cancel);

        await Assert.That(result.IfFail(out var error)).IsTrue();
        await Assert.That(error!.Code).IsEqualTo(StandardErrorCodes.DatabaseTransactionError).Because("someone's ID was overwritten");
    }
}

public class Upsert
{
    [Test]
    [DisplayName("Upsert New Kid")]
    public async Task UpsertNewKid(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        TimeProvider time = new FixedClock(NewYear2024);

        // when
        var result = await ThrowIfError(customer.Upsert<Customer, Guid>(NewKid, clock: time, cancel: cancel));

        // then
        var expect = NewKid with { Updated = NewYear2024, Version = 2u };
        var db = await customer.GetById(NewKid.Id, cancel: cancel);
        var cursor = await customer.FindAsync(x => x.Address.Country == "US", cancellationToken: cancel);
        var allUsPeople = await ThrowIfError(cursor.Retrieve(async x => await x.ExecuteList()));
        await Assert.That(result).IsEquivalentTo(expect);
        await Assert.That(db.Unwrap()).IsEquivalentTo(expect);
        await Assert.That(allUsPeople.Count).IsEqualTo(2);
        await Assert.That(allUsPeople).Contains(expect);
    }

    [Test]
    [DisplayName("Try upsert the existing Jane won't have any change and no error")]
    public async Task TryUpsertTheExistingJaneWontHaveAnyChangeAndNoError(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        // when
        var result = await customer.Upsert<Customer, Guid>(JaneDoe, cancel: cancel);

        // then
        await Assert.That(result.IsSuccess).IsTrue();

        var allThPeople = await customer.FindAsync(x => x.Address.Country == "TH", cancellationToken: cancel)
                                        .Retrieve(async x => await x.ExecuteList());
        await Assert.That(allThPeople.Unwrap().Count).IsEqualTo(2).Because("no new record was added");
    }

    [Test]
    [DisplayName("Upsert Jane Zip code")]
    public async Task UpsertJaneZipCode(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        // when
        var result = await customer.Upsert(JaneDoe.Id, JaneDoe with { Address = JaneDoe.Address with { Zip = "22222" } }, cancel: cancel);

        // then
        var expect = JaneDoe with { Address = JaneDoe.Address with { Zip = "22222" } };
        var db = await customer.GetById(JaneDoe.Id, cancel: cancel);
        await Assert.That(result.Unwrap()).IsEquivalentTo(expect);
        await Assert.That(db.Unwrap()).IsEquivalentTo(expect);
    }

    [Test]
    [DisplayName("Upsert Jane Zip code with outdated explicit version number, results in duplication")]
    public async Task UpsertJaneZipCodeWithOutdatedExplicitVersionNumberResultsInDuplication(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        // when
        var updatedJane = JaneDoe with { Address = JaneDoe.Address with { Zip = "22222" } };
        var result = await customer.Upsert(JaneDoe.Id, updatedJane, 123u, cancel: cancel);

        // then
        await Assert.That(result.IsFail).IsTrue();
        await Assert.That(result.UnwrapError().Code).IsEqualTo(StandardErrorCodes.Duplication); // note that this is different from Update where it gets Race Condition!
    }

    [Test]
    [DisplayName("Try upsert Jane Zip code with outdated explicit version number, results in duplication")]
    public async Task TryUpsertJaneZipCodeWithOutdatedExplicitVersionNumberResultsInDuplication(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        // when
        var updatedJane = JaneDoe with { Address = JaneDoe.Address with { Zip = "22222" } };
        var result = await customer.Upsert(JaneDoe.Id, updatedJane, 123u, cancel: cancel);

        // then
        await Assert.That(result.IfFail(out var error, out _)).IsTrue();
        await Assert.That(error!.Code).IsEqualTo(StandardErrorCodes.Duplication);
    }

    [Test]
    [DisplayName("Upsert John zip code with his unique zip must succeed")]
    public async Task UpsertJohnZipCodeWithHisUniqueZipMustSucceed(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        // when
        var updatedJohn = JohnDoe with { Address = JohnDoe.Address with { Zip = "22222" } };
        var result = await customer.Upsert(updatedJohn, x => x.Address.Zip == "11111", cancel: cancel);

        var john = await ThrowIfError(customer.GetById(JohnDoe.Id, cancel: cancel));
        await Assert.That(john).IsEquivalentTo(updatedJohn);
        await Assert.That(result.Unwrap()).IsEquivalentTo(updatedJohn);
    }

    [Test]
    [DisplayName("Try upsert John zip code with his invalid zip will fail from inserting a duplicated record")]
    public async Task TryUpsertJohnZipCodeWithHisInvalidZipWillFailFromInsertingADuplicatedRecord(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        // when
        var updatedJohn = JohnDoe with { Address = JohnDoe.Address with { Zip = "22222" } };
        var result = await customer.Upsert(updatedJohn, x => x.Address.Zip == "99999", cancel: cancel);

        // then
        await Assert.That(result.IfFail(out var error, out _)).IsTrue();
        await Assert.That(error!.Code).IsEqualTo(StandardErrorCodes.Duplication);
    }
}

public class Deletion
{
    [Test]
    [DisplayName("Delete all customers!")]
    public async Task DeleteAllCustomers(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        // when
        await customer.DeleteAll(_ => true, cancel: cancel);

        // then
        var people = await customer.FindAsync(_ => true, cancellationToken: cancel)
                                   .Retrieve(async x => await x.ExecuteList());
        await Assert.That(people.Unwrap().Count).IsEqualTo(0);
    }

    [Test]
    [DisplayName("Delete Jane")]
    public async Task DeleteJane(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        // when
        await customer.Delete<Customer, Guid>(JaneDoe, cancel: cancel);

        // then
        var jane = await customer.GetById(JaneDoe.Id, cancel: cancel);
        await Assert.That(jane.IsFail).IsTrue();
        await Assert.That(jane.UnwrapError().Code).IsEqualTo(StandardErrorCodes.NotFound);
    }

    [Test]
    [DisplayName("Delete with unique zip condition")]
    public async Task DeleteWithUniqueZipCondition(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        // when
        await customer.Delete(x => x.Address.Zip == UniqueZip, cancel: cancel);

        // then
        var john = await customer.GetById(JohnDoe.Id, cancel: cancel);
        await Assert.That(john.IsFail).IsTrue();
        await Assert.That(john.UnwrapError().Code).IsEqualTo(StandardErrorCodes.NotFound);
    }

    [Test]
    [DisplayName("Delete with a specific key")]
    public async Task DeleteWithSpecificKey(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        // when
        await customer.Delete(JohnDoe.Id, cancel: cancel);

        // then
        var john = await customer.GetById(JohnDoe.Id, cancel: cancel);
        await Assert.That(john.IsFail).IsTrue();
        await Assert.That(john.UnwrapError().Code).IsEqualTo(StandardErrorCodes.NotFound);
    }

    [Test]
    [DisplayName("Delete with a key and an invalid version, should have no effect")]
    public async Task DeleteWithKeyAndInvalidVersionShouldHaveNoEffect(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();
        var customerCount = await customer.CountDocumentsAsync(_ => true, cancellationToken: cancel);

        // when
        await customer.Delete(JohnDoe.Id, 123u, cancel: cancel);

        // then
        var currentCount = await customer.CountDocumentsAsync(_ => true, cancellationToken: cancel);
        await Assert.That(currentCount).IsEqualTo(customerCount);
    }

    [Test]
    [DisplayName("Try deleting all customers!")]
    public async Task TryDeletingAllCustomers(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        // when
        var result = await customer.DeleteAll(_ => true, cancel: cancel);

        // then
        await Assert.That(result.IsSuccess).IsTrue();

        var people = await customer.FindAsync(_ => true, cancellationToken: cancel)
                                   .Retrieve(async x => await x.ExecuteList());
        await Assert.That(people.Unwrap().Count).IsEqualTo(0);
    }

    [Test]
    [DisplayName("Try deleting Jane")]
    public async Task TryDeletingJane(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        // when
        var result = await customer.Delete<Customer, Guid>(JaneDoe, cancel: cancel);

        // then
        await Assert.That(result.IsSuccess).IsTrue();

        var jane = await customer.GetById(JaneDoe.Id, cancel: cancel);
        await Assert.That(jane.IsFail).IsTrue();
        await Assert.That(jane.UnwrapError().Code).IsEqualTo(StandardErrorCodes.NotFound);
    }

    [Test]
    [DisplayName("Try deleting with multiple matches, only (random) one is removed")]
    public async Task TryDeletingWithMultipleMatchesOnlyOneIsRemoved(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        // when
        var result = await customer.Delete(x => x.Address.Zip == "10000", cancel: cancel);

        // then
        await Assert.That(result.IsSuccess).IsTrue();

        var people = await customer.FindAsync(x => x.Address.Zip == "10000", cancellationToken: cancel)
                                   .Retrieve(async x => await x.ExecuteList());
        await Assert.That(people.Unwrap().Count).IsEqualTo(1);
    }

    [Test]
    [DisplayName("Try deleting with a specific key")]
    public async Task TryDeletingWithSpecificKey(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        // when
        var result = await customer.Delete(JohnDoe.Id, cancel: cancel);

        // then
        await Assert.That(result.IsSuccess).IsTrue();
        var john = await customer.GetById(JohnDoe.Id, cancel: cancel);
        await Assert.That(john.IsFail).IsTrue();
        await Assert.That(john.UnwrapError().Code).IsEqualTo(StandardErrorCodes.NotFound);
    }

    [Test]
    [DisplayName("Try deleting with a key and an invalid version, should have no effect")]
    public async Task TryDeletingWithKeyAndInvalidVersionShouldHaveNoEffect(CancellationToken cancel) {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();
        var customerCount = await customer.CountDocumentsAsync(_ => true, cancellationToken: cancel);

        // when
        var result = await customer.Delete(JohnDoe.Id, 123u, cancel: cancel);

        // then
        await Assert.That(result.IsSuccess).IsTrue();
        var currentCount = await customer.CountDocumentsAsync(_ => true, cancellationToken: cancel);
        await Assert.That(currentCount).IsEqualTo(customerCount);
    }
}

sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
