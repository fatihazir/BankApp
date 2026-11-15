using Application.Features.CorporateCustomers.Commands.Create;
using Application.Features.CorporateCustomers.Commands.Update;
using Application.Features.CorporateCustomers.Profiles;
using BankingSystemCore.Repositories;
using Domain.Entities;

namespace Application.UnitTests.Features.CorporateCustomers;

public class CorporateCustomerMapperTests
{
    [Fact]
    public void ToEntity_CopiesCommandValues_AndLeavesSystemFieldsEmpty()
    {
        var command = new CreateCorporateCustomerCommand
        {
            CompanyName = "Acme",
            TaxNumber = "1234567890",
            FoundationDate = new DateOnly(2010, 5, 1),
            AnnualRevenue = 12_500_000.75m
        };

        CorporateCustomer entity = CorporateCustomerMapper.ToEntity(command);

        entity.CompanyName.ShouldBe("Acme");
        entity.FoundationDate.ShouldBe(new DateOnly(2010, 5, 1));
        entity.AnnualRevenue.ShouldBe(12_500_000.75m);
        entity.Id.ShouldBe(Guid.Empty);
        entity.CustomerNumber.ShouldBeEmpty();
    }

    [Fact]
    public void Apply_UpdatesEntity_WithoutTouchingSystemManagedFields()
    {
        var entity = new CorporateCustomer { CustomerNumber = "COR-ABC", CreditScore = 800, CompanyName = "Old" };

        CorporateCustomerMapper.Apply(
            new UpdateCorporateCustomerCommand { Id = Guid.NewGuid(), CompanyName = "New", IsActive = true }, entity);

        entity.CompanyName.ShouldBe("New");
        entity.CustomerNumber.ShouldBe("COR-ABC");
        entity.CreditScore.ShouldBe(800);
    }

    [Fact]
    public void ToListResponse_MapsPagingInformationAndItems()
    {
        var page = new Paginate<CorporateCustomer>
        {
            Index = 0,
            Size = 10,
            Count = 1,
            Pages = 1,
            Items = [new CorporateCustomer { CompanyName = "Acme" }]
        };

        var result = CorporateCustomerMapper.ToListResponse(page);

        result.Count.ShouldBe(1);
        result.HasNext.ShouldBeFalse();
        result.Items[0].CompanyName.ShouldBe("Acme");
    }
}
