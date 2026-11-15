using Application.Features.IndividualCustomers.Commands.Create;
using Application.Features.IndividualCustomers.Commands.Delete;
using Application.Features.IndividualCustomers.Commands.Update;
using Application.Features.IndividualCustomers.Queries.GetById;
using Application.Features.IndividualCustomers.Queries.GetList;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Persistence.IntegrationTests.Infrastructure;

namespace Persistence.IntegrationTests.Customers;

[Collection(DatabaseCollection.Name)]
public class IndividualCustomerTests(SqlServerFixture database) : IntegrationTestBase(database)
{
    private static UpdateIndividualCustomerCommand UpdateFor(Guid id, int nationalIdSeed = 1) => new()
    {
        Id = id,
        PhoneNumber = "5551112233",
        Address = "Street 2",
        City = "Ankara",
        Country = "TR",
        IsActive = true,
        FirstName = "Ayse",
        LastName = "Veli",
        NationalId = TestData.NationalId(nationalIdSeed),
        DateOfBirth = new DateOnly(1990, 1, 1),
        EmploymentStatus = EmploymentStatus.SelfEmployed,
        MonthlyIncome = 60000m
    };

    [Fact]
    public async Task Create_ThenGetById_ReturnsStoredCustomer()
    {
        var created = await Send(TestData.IndividualCreate());

        created.Id.ShouldNotBe(Guid.Empty);
        created.CustomerNumber.ShouldStartWith("IND-");
        created.CreateDate.ShouldNotBe(default);

        var customer = await Send(new GetByIdIndividualCustomerQuery { Id = created.Id });
        customer.FirstName.ShouldBe("Ali");
        customer.MonthlyIncome.ShouldBe(50000m);
        customer.DateOfBirth.ShouldBe(new DateOnly(1990, 1, 1));
        customer.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task GetList_ReturnsRequestedPage()
    {
        for (int i = 1; i <= 3; i++) await Send(TestData.IndividualCreate(i));

        var first = await Send(new GetListIndividualCustomerQuery { Index = 0, Size = 2 });
        first.Count.ShouldBe(3);
        first.Pages.ShouldBe(2);
        first.Items.Count.ShouldBe(2);
        first.HasNext.ShouldBeTrue();
        first.HasPrevious.ShouldBeFalse();

        var second = await Send(new GetListIndividualCustomerQuery { Index = 1, Size = 2 });
        second.Items.Count.ShouldBe(1);
        second.HasNext.ShouldBeFalse();
        second.HasPrevious.ShouldBeTrue();
    }

    [Fact]
    public async Task TheDates_ComeFromTheClock_OnCreateUpdateAndDelete()
    {
        var created = new DateTimeOffset(2030, 1, 1, 10, 0, 0, TimeSpan.Zero);
        Clock.Now = created;
        Guid id = (await Send(TestData.IndividualCreate())).Id;

        Clock.Advance(TimeSpan.FromDays(1));
        await Send(UpdateFor(id));

        Clock.Advance(TimeSpan.FromDays(1));
        await Send(new DeleteIndividualCustomerCommand { Id = id });

        var row = await UsingDbContext(db => db.IndividualCustomers.AsNoTracking().SingleAsync(c => c.Id == id));
        (row.CreateDate, row.UpdateDate, row.DeleteDate).ShouldBe(
            (created.UtcDateTime, created.AddDays(1).UtcDateTime, created.AddDays(2).UtcDateTime));
    }

    [Fact]
    public async Task Update_ChangesFields_ButKeepsCustomerNumber()
    {
        var created = await Send(TestData.IndividualCreate());

        var updated = await Send(UpdateFor(created.Id));
        updated.UpdateDate.ShouldNotBeNull();

        var customer = await Send(new GetByIdIndividualCustomerQuery { Id = created.Id });
        customer.FirstName.ShouldBe("Ayse");
        customer.City.ShouldBe("Ankara");
        customer.CustomerNumber.ShouldBe(created.CustomerNumber);
        customer.CreateDate.ShouldBe(created.CreateDate);
    }

    [Fact]
    public async Task Create_WithDuplicateNationalId_IsRejected()
    {
        await Send(TestData.IndividualCreate());

        await Should.ThrowAsync<BusinessException>(() => Send(TestData.IndividualCreate()));
    }

    [Fact]
    public async Task Update_ToAnotherCustomersNationalId_IsRejected()
    {
        var first = await Send(TestData.IndividualCreate(1));
        await Send(TestData.IndividualCreate(2));

        await Should.ThrowAsync<BusinessException>(() => Send(UpdateFor(first.Id, nationalIdSeed: 2)));
    }

    [Fact]
    public async Task Delete_IsSoft_RowStaysInDatabaseButIsHiddenFromQueries()
    {
        var created = await Send(TestData.IndividualCreate());

        var deleted = await Send(new DeleteIndividualCustomerCommand { Id = created.Id });
        deleted.DeleteDate.ShouldNotBeNull();

        await Should.ThrowAsync<NotFoundException>(() => Send(new GetByIdIndividualCustomerQuery { Id = created.Id }));
        (await Send(new GetListIndividualCustomerQuery())).Count.ShouldBe(0);

        var row = await UsingDbContext(db => db.IndividualCustomers.AsNoTracking().SingleAsync(c => c.Id == created.Id));
        row.DeleteDate.ShouldNotBeNull();
    }

    [Fact]
    public async Task Create_WithNationalIdOfSoftDeletedCustomer_IsRejected()
    {
        var created = await Send(TestData.IndividualCreate());
        await Send(new DeleteIndividualCustomerCommand { Id = created.Id });

        await Should.ThrowAsync<BusinessException>(() => Send(TestData.IndividualCreate()));
    }
}
