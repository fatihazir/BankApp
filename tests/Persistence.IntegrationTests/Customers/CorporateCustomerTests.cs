using Application.Features.CorporateCustomers.Commands.Create;
using Application.Features.CorporateCustomers.Commands.Delete;
using Application.Features.CorporateCustomers.Commands.Update;
using Application.Features.CorporateCustomers.Queries.GetById;
using Application.Features.CorporateCustomers.Queries.GetList;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using Microsoft.EntityFrameworkCore;
using Persistence.IntegrationTests.Infrastructure;

namespace Persistence.IntegrationTests.Customers;

[Collection(DatabaseCollection.Name)]
public class CorporateCustomerTests(SqlServerFixture database) : IntegrationTestBase(database)
{
    private static UpdateCorporateCustomerCommand UpdateFor(Guid id, int taxSeed = 1) => new()
    {
        Id = id,
        PhoneNumber = "2121112233",
        Address = "Plaza 2",
        City = "Izmir",
        Country = "TR",
        IsActive = true,
        CompanyName = "Acme Holding",
        TaxNumber = TestData.TaxNumber(taxSeed),
        TaxOffice = "Kadikoy",
        FoundationDate = new DateOnly(2010, 5, 1),
        EmployeeCount = 60,
        AnnualRevenue = 1m,
        AuthorizedPersonName = "Jane Doe"
    };

    [Fact]
    public async Task Create_ThenGetById_ReturnsStoredCustomer_WithDecimalPrecision()
    {
        var created = await Send(TestData.CorporateCreate());
        created.CustomerNumber.ShouldStartWith("COR-");

        var customer = await Send(new GetByIdCorporateCustomerQuery { Id = created.Id });
        customer.CompanyName.ShouldBe("Acme");
        customer.AnnualRevenue.ShouldBe(12_500_000.75m);
        customer.FoundationDate.ShouldBe(new DateOnly(2010, 5, 1));
    }

    [Fact]
    public async Task GetList_ReturnsRequestedPage()
    {
        for (int i = 1; i <= 3; i++) await Send(TestData.CorporateCreate(i));

        var page = await Send(new GetListCorporateCustomerQuery { Index = 0, Size = 2 });

        page.Count.ShouldBe(3);
        page.Pages.ShouldBe(2);
        page.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Update_ChangesFields_ButKeepsCustomerNumber()
    {
        var created = await Send(TestData.CorporateCreate());

        var updated = await Send(UpdateFor(created.Id));
        updated.CompanyName.ShouldBe("Acme Holding");
        updated.UpdateDate.ShouldNotBeNull();

        var customer = await Send(new GetByIdCorporateCustomerQuery { Id = created.Id });
        customer.CustomerNumber.ShouldBe(created.CustomerNumber);
        customer.City.ShouldBe("Izmir");
    }

    [Fact]
    public async Task Create_WithDuplicateTaxNumber_IsRejected()
    {
        await Send(TestData.CorporateCreate());

        await Should.ThrowAsync<BusinessException>(() => Send(TestData.CorporateCreate()));
    }

    [Fact]
    public async Task Update_ToAnotherCustomersTaxNumber_IsRejected()
    {
        var first = await Send(TestData.CorporateCreate(1));
        await Send(TestData.CorporateCreate(2));

        await Should.ThrowAsync<BusinessException>(() => Send(UpdateFor(first.Id, taxSeed: 2)));
    }

    [Fact]
    public async Task Create_WithFutureFoundationDate_IsRejected()
    {
        var command = TestData.CorporateCreate();
        command.FoundationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));

        await Should.ThrowAsync<BusinessException>(() => Send(command));
    }

    [Fact]
    public async Task Create_WithWrongTaxNumberLength_FailsValidation()
    {
        var command = TestData.CorporateCreate();
        command.TaxNumber = "123";

        await Should.ThrowAsync<ValidationException>(() => Send(command));
    }

    [Fact]
    public async Task Delete_IsSoft_RowStaysInDatabaseButIsHiddenFromQueries()
    {
        var created = await Send(TestData.CorporateCreate());

        await Send(new DeleteCorporateCustomerCommand { Id = created.Id });

        (await Send(new GetListCorporateCustomerQuery())).Count.ShouldBe(0);
        var row = await UsingDbContext(db => db.CorporateCustomers.AsNoTracking().SingleAsync(c => c.Id == created.Id));
        row.DeleteDate.ShouldNotBeNull();
    }

    [Fact]
    public async Task Create_WithTaxNumberOfSoftDeletedCustomer_IsRejected()
    {
        var created = await Send(TestData.CorporateCreate());
        await Send(new DeleteCorporateCustomerCommand { Id = created.Id });

        await Should.ThrowAsync<BusinessException>(() => Send(TestData.CorporateCreate()));
    }
}
