using FluentAssertions;
using MongoDB.Driver;
using Moq;
using RZ.Foundation;
using RZ.Foundation.MongoDb;
using RZ.Foundation.Types;
using static UnitTests.TestSample;

namespace UnitTests;

public class Add
{
    [Fact(DisplayName = "Add single row and query")]
    public async Task AddSingleRowAndQuery() {
        var person = new Customer("John Doe", new("TH", "10000"), 0, new(2024, 1, 31, 17, 0, 0, TimeSpan.Zero), JohnDoe.Id);

        // when
        var mdb = MockDb.StartDb();
        await mdb.Db.GetCollection<Customer>().Add(person);

        // then
        var result = await mdb.Db.GetCollection<Customer>().GetById(person.Id);
        result.Should().BeEquivalentTo(person);
    }

    [Fact(DisplayName = "Repeatedly add the same single row will throw")]
    public async Task RepeatedlyAddTheSameSingleRowWillThrow() {
        var person = new Customer("John Doe", new Address("TH", "10000"), 0, new DateTimeOffset(2024, 1, 31, 17, 0, 0, TimeSpan.Zero), JohnDoe.Id);

        // when
        var mdb = MockDb.StartDb();
        var coll = mdb.Db.GetCollection<Customer>();
        await coll.Add(person);

        // then when inserting the same record the second time
        Func<Task> result = () => coll.Add(person);

        var exception = await result.Should().ThrowAsync<ErrorInfoException>();

        exception.Which.Code.Should().Be(StandardErrorCodes.Duplication);
    }

    [Fact(DisplayName = "Capture duplicated add error with TryAdd")]
    public async Task CaptureDuplicatedAddErrorWithTryAdd() {
        var mdb = MockDb.StartWithSample();

        // when
        var result = await mdb.Db.GetCollection<Customer>().TryAdd(new Customer("Example Name", new("TH", "10000"), 0, new(2020, 1, 1, 17, 0, 0, TimeSpan.Zero), new("711CA94D-239C-4E67-81C9-1F2F155B3F43")));

        // then
        result.IfFail(out var error, out _).Should().BeTrue();
        error.Code.Should().Be(StandardErrorCodes.Duplication);
    }

    [Fact(DisplayName = "Simple add with TryAdd")]
    public async Task SimpleAddWithTryAdd() {
        var mdb = MockDb.StartWithSample();

        // when
        var result = await mdb.Db.GetCollection<Customer>().TryAdd(new("Testla Namera", new("XY", "10000"), 0, new(2020, 1, 1, 17, 0, 0, TimeSpan.Zero), UnusedGuid1));

        // then
        result.IsSuccess.Should().BeTrue();
    }
}

public class Retrieval
{
    [Fact(DisplayName = "Get the first customer with zip code 11111")]
    public async Task GetFirstCustomerWithZipCode11111() {
        var mdb = MockDb.StartWithSample();

        // when
        var result = await mdb.Db.GetCollection<Customer>().Get(x => x.Address.Zip == "11111");

        // then
        result.Should().BeEquivalentTo(
            new Customer("John Doe",
                         new Address("TH", "11111"),
                         1,
                         new DateTimeOffset(2020, 1, 1, 17, 0, 0, TimeSpan.Zero),
                         new Guid("0B8D9631-720A-46B7-8C95-F55B4EC520A4")
                ));
    }

    [Fact(DisplayName = "Get all customers with country 'TH'")]
    public async Task GetAllCustomersWithCountryTh() {
        var mdb = MockDb.StartWithSample();

        // when
        var result = await mdb.Db.GetCollection<Customer>().FindAsync(x => x.Address.Country == "TH").Retrieve(x => x.ToListAsync());

        // then
        result.Count.Should().Be(2);

        var names = result.Select(x => x.Name);
        names.Should().BeEquivalentTo(JohnDoe.Name, JaneDoe.Name);
    }
}

public class Update
{
    [Fact(DisplayName = "Update Jane's zip code")]
    public async Task UpdateJaneZipCode()
    {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        var time = new Mock<TimeProvider>();
        time.Setup(x => x.GetUtcNow()).Returns(NewYear2024);

        // when
        var jane = await customer.GetById(JaneDoe.Id);
        var updatedJane = jane! with { Address = jane.Address with { Zip = "22222" } };
        await customer.Update<Customer, Guid>(updatedJane, clock: time.Object);

        // then
        var expected = updatedJane with { Updated = NewYear2024, Version = 3u };
        jane = await customer.GetById(JaneDoe.Id);
        jane.Should().BeEquivalentTo(expected);
    }

    [Fact(DisplayName = "Try updating Jane Zip code must succeed")]
    public async Task TryUpdatingJaneZipCodeMustSucceed()
    {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        var time = new Mock<TimeProvider>();
        time.Setup(x => x.GetUtcNow()).Returns(NewYear2024);

        // when
        var jane = await customer.GetById(JaneDoe.Id);
        var updatedJane = jane! with { Address = jane.Address with { Zip = "22222" } };
        var result = await customer.TryUpdate<Customer, Guid>(updatedJane, clock: time.Object);

        // then
        result.IsSuccess.Should().BeTrue();

        var expected = updatedJane with { Updated = NewYear2024, Version = 3u };
        jane = await customer.GetById(JaneDoe.Id);
        jane.Should().BeEquivalentTo(expected);
    }

    [Fact(DisplayName = "Update Jane Zip code with explicit version number")]
    public async Task UpdateJaneZipCodeWithExplicitVersionNumber()
    {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        // when
        var updatedJane = JaneDoe with { Address = JaneDoe.Address with { Zip = "22222" } };
        await customer.Update(JaneDoe.Id, updatedJane, JaneDoe.Version);

        // then
        var jane = await customer.GetById(JaneDoe.Id);
        jane.Should().BeEquivalentTo(updatedJane);
    }

    [Fact(DisplayName = "Update Jane Zip code with outdated explicit version number, results in race condition")]
    public async Task UpdateJaneZipCodeWithOutdatedExplicitVersionNumberResultsInRaceCondition()
    {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        // when
        var updatedJane = JaneDoe with { Address = JaneDoe.Address with { Zip = "22222" } };
        Func<Task> action = () => customer.Update(JaneDoe.Id, updatedJane, 123u);

        // then
        var error = await action.Should().ThrowAsync<ErrorInfoException>();

        error.Which.Code.Should().Be(StandardErrorCodes.RaceCondition);
    }

    [Fact(DisplayName = "Try updating Jane Zip code with outdated explicit version number, results in race condition")]
    public async Task TryUpdatingJaneZipCodeWithOutdatedExplicitVersionNumberResultsInRaceCondition()
    {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        var updatedJane = JaneDoe with { Address = JaneDoe.Address with { Zip = "22222" } };
        var result = await customer.TryUpdate(JaneDoe.Id, updatedJane, 123u);

        result.IfFail(out var error, out _).Should().BeTrue();
        error.Code.Should().Be(StandardErrorCodes.RaceCondition);
    }

    [Fact(DisplayName = "Update Jane Zip code with the explicit (new) key and data's key mismatch, results in race condition error")]
    public async Task UpdateJaneZipCodeWithExplicitNewKeyAndDataKeyMismatchResultsInRaceConditionError()
    {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        var updatedJane = JaneDoe with { Address = JaneDoe.Address with { Zip = "22222" } };
        Func<Task> action = () => customer.Update(UnusedGuid1, updatedJane);

        var error = await action.Should().ThrowAsync<ErrorInfoException>();

        error.Which.Code.Should().Be(StandardErrorCodes.RaceCondition);
    }

    [Fact(DisplayName = "Update Jane Zip code with the explicit (valid) key and data's key mismatch, results in database transaction error")]
    public async Task UpdateJaneZipCodeWithExplicitValidKeyAndDataKeyMismatchResultsInDatabaseTransactionError()
    {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        var updatedJane = JaneDoe with { Address = JaneDoe.Address with { Zip = "22222" } };
        Func<Task> action = () => customer.Update(JohnDoe.Id, updatedJane);

        var error = await action.Should().ThrowAsync<ErrorInfoException>();

        error.Which.Code.Should().Be(StandardErrorCodes.DatabaseTransactionError);
    }

    [Fact(DisplayName = "Update John zip code with his *unique* zip")]
    public async Task UpdateJohnZipCodeWithHisUniqueZip()
    {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        var updatedJohn = JohnDoe with { Address = JohnDoe.Address with { Zip = "22222" } };
        await customer.Update(updatedJohn, x => x.Address.Zip == "11111");

        var john = await customer.GetById(JohnDoe.Id);
        john.Should().BeEquivalentTo(updatedJohn);
    }

    [Fact(DisplayName = "Update with multiple matches will result in ID overwritten which will fail")]
    public async Task UpdateWithMultipleMatchesWillResultInIDOverwrittenWhichWillFail()
    {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        Func<Task> action = () => customer.Update(NewKid, x => x.Address.Country == "TH");

        var error = await action.Should().ThrowAsync<ErrorInfoException>();

        error.Which.Code.Should().Be(StandardErrorCodes.DatabaseTransactionError, "someone's ID was overwritten");
    }

    [Fact(DisplayName = "Try updating with multiple matches will result in ID overwritten which will fail")]
    public async Task TryUpdatingWithMultipleMatchesWillResultInIDOverwrittenWhichWillFail()
    {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        var result = await customer.TryUpdate(NewKid, x => x.Address.Country == "TH");

        result.IfFail(out var error, out _).Should().BeTrue();
        error.Code.Should().Be(StandardErrorCodes.DatabaseTransactionError, "someone's ID was overwritten");
    }
}

public class Upsert
{
    [Fact(DisplayName = "Upsert New Kid")]
    public async Task UpsertNewKid()
    {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        var time = new Mock<TimeProvider>();
        time.Setup(x => x.GetUtcNow()).Returns(NewYear2024);

        // when
        var result = await customer.Upsert<Customer, Guid>(NewKid, clock: time.Object);

        // then
        var expect = NewKid with { Updated = NewYear2024, Version = 2u };
        var db = await customer.GetById(NewKid.Id);
        var cursor = await customer.FindAsync(x => x.Address.Country == "US");
        var allUsPeople = await cursor.Retrieve(x => x.ToListAsync());
        result.Should().BeEquivalentTo(expect);
        db.Should().BeEquivalentTo(expect);
        allUsPeople.Count.Should().Be(2);
        allUsPeople.Should().Contain(expect);
    }

    [Fact(DisplayName = "Try upsert the existing Jane won't have any change and no error")]
    public async Task TryUpsertTheExistingJaneWontHaveAnyChangeAndNoError()
    {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        // when
        var result = await customer.TryUpsert<Customer, Guid>(JaneDoe);

        // then
        result.IsSuccess.Should().BeTrue();

        var allThPeople = await customer.FindAsync(x => x.Address.Country == "TH").Retrieve(x => x.ToListAsync());
        allThPeople.Count.Should().Be(2, "no new record was added");
    }

    [Fact(DisplayName = "Upsert Jane Zip code")]
    public async Task UpsertJaneZipCode()
    {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        // when
        var result = await customer.Upsert(JaneDoe.Id, JaneDoe with { Address = JaneDoe.Address with { Zip = "22222" } });

        // then
        var expect = JaneDoe with { Address = JaneDoe.Address with { Zip = "22222" } };
        var db = await customer.GetById(JaneDoe.Id);
        result.Should().BeEquivalentTo(expect);
        db.Should().BeEquivalentTo(expect);
    }

    [Fact(DisplayName = "Upsert Jane Zip code with outdated explicit version number, results in duplication")]
    public async Task UpsertJaneZipCodeWithOutdatedExplicitVersionNumberResultsInDuplication()
    {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        // when
        var updatedJane = JaneDoe with { Address = JaneDoe.Address with { Zip = "22222" } };
        Func<Task> action = () => customer.Upsert(JaneDoe.Id, updatedJane, 123u);

        // then
        var error = await action.Should().ThrowAsync<ErrorInfoException>();

        error.Which.Code.Should().Be(StandardErrorCodes.Duplication); // note that this is different from Update where it gets Race Condition!
    }

    [Fact(DisplayName = "Try upsert Jane Zip code with outdated explicit version number, results in duplication")]
    public async Task TryUpsertJaneZipCodeWithOutdatedExplicitVersionNumberResultsInDuplication()
    {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        // when
        var updatedJane = JaneDoe with { Address = JaneDoe.Address with { Zip = "22222" } };
        var result = await customer.TryUpsert(JaneDoe.Id, updatedJane, 123u);

        // then
        result.IfFail(out var error, out _).Should().BeTrue();
        error.Code.Should().Be(StandardErrorCodes.Duplication);
    }

    [Fact(DisplayName = "Upsert John zip code with his unique zip must succeed")]
    public async Task UpsertJohnZipCodeWithHisUniqueZipMustSucceed()
    {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        // when
        var updatedJohn = JohnDoe with { Address = JohnDoe.Address with { Zip = "22222" } };
        var result = await customer.Upsert(updatedJohn, x => x.Address.Zip == "11111");

        var john = await customer.GetById(JohnDoe.Id);
        john.Should().BeEquivalentTo(updatedJohn);
        result.Should().BeEquivalentTo(updatedJohn);
    }

    [Fact(DisplayName = "Try upsert John zip code with his invalid zip will fail from inserting a duplicated record")]
    public async Task TryUpsertJohnZipCodeWithHisInvalidZipWillFailFromInsertingADuplicatedRecord()
    {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        // when
        var updatedJohn = JohnDoe with { Address = JohnDoe.Address with { Zip = "22222" } };
        var result = await customer.TryUpsert(updatedJohn, x => x.Address.Zip == "99999");

        // then
        result.IfFail(out var error, out _).Should().BeTrue();
        error.Code.Should().Be(StandardErrorCodes.Duplication);
    }
}

public class Deletion
{
    [Fact(DisplayName = "Delete all customers!")]
    public async Task DeleteAllCustomers()
    {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        // when
        await customer.DeleteAll(_ => true);

        // then
        var people = await customer.FindAsync(_ => true).Retrieve(_ => _.ToListAsync());
        people.Count.Should().Be(0);
    }

    [Fact(DisplayName = "Delete Jane")]
    public async Task DeleteJane()
    {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        // when
        await customer.Delete<Customer, Guid>(JaneDoe);

        // then
        var jane = await customer.GetById(JaneDoe.Id);
        jane.Should().BeNull();
    }

    [Fact(DisplayName = "Delete with unique zip condition")]
    public async Task DeleteWithUniqueZipCondition()
    {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        // when
        await customer.Delete(x => x.Address.Zip == UniqueZip);

        // then
        var john = await customer.GetById(JohnDoe.Id);
        john.Should().BeNull();
    }

    [Fact(DisplayName = "Delete with a specific key")]
    public async Task DeleteWithSpecificKey()
    {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        // when
        await customer.Delete(JohnDoe.Id);

        // then
        var john = await customer.GetById(JohnDoe.Id);
        john.Should().BeNull();
    }

    [Fact(DisplayName = "Delete with a key and an invalid version, should have no effect")]
    public async Task DeleteWithKeyAndInvalidVersionShouldHaveNoEffect()
    {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();
        var customerCount = await customer.CountDocumentsAsync(_ => true);

        // when
        await customer.Delete(JohnDoe.Id, 123u);

        // then
        var currentCount = await customer.CountDocumentsAsync(_ => true);
        currentCount.Should().Be(customerCount);
    }

    [Fact(DisplayName = "Try deleting all customers!")]
    public async Task TryDeletingAllCustomers()
    {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        // when
        var result = await customer.TryDeleteAll(_ => true);

        // then
        result.IsSuccess.Should().BeTrue();

        var people = await customer.FindAsync(_ => true).Retrieve(_ => _.ToListAsync());
        people.Count.Should().Be(0);
    }

    [Fact(DisplayName = "Try deleting Jane")]
    public async Task TryDeletingJane()
    {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        // when
        var result = await customer.TryDelete<Customer, Guid>(JaneDoe);

        // then
        result.IsSuccess.Should().BeTrue();

        var jane = await customer.GetById(JaneDoe.Id);
        jane.Should().BeNull();
    }

    [Fact(DisplayName = "Try deleting with multiple matches, only (random) one is removed")]
    public async Task TryDeletingWithMultipleMatchesOnlyOneIsRemoved()
    {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        // when
        var result = await customer.TryDelete(x => x.Address.Zip == "10000");

        // then
        result.IsSuccess.Should().BeTrue();

        var people = await customer.FindAsync(x => x.Address.Zip == "10000").Retrieve(_ => _.ToListAsync());
        people.Count.Should().Be(1);
    }

    [Fact(DisplayName = "Try deleting with a specific key")]
    public async Task TryDeletingWithSpecificKey()
    {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();

        // when
        var result = await customer.TryDelete(JohnDoe.Id);

        // then
        result.IsSuccess.Should().BeTrue();
        var john = await customer.GetById(JohnDoe.Id);
        john.Should().BeNull();
    }

    [Fact(DisplayName = "Try deleting with a key and an invalid version, should have no effect")]
    public async Task TryDeletingWithKeyAndInvalidVersionShouldHaveNoEffect()
    {
        var mdb = MockDb.StartWithSample();
        var customer = mdb.Db.GetCollection<Customer>();
        var customerCount = await customer.CountDocumentsAsync(_ => true);

        // when
        var result = await customer.TryDelete(JohnDoe.Id, 123u);

        // then
        result.IsSuccess.Should().BeTrue();
        var currentCount = await customer.CountDocumentsAsync(_ => true);
        currentCount.Should().Be(customerCount);
    }
}