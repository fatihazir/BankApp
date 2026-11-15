using Application.Features.IndividualCustomers.Commands.Create;
using Application.Features.IndividualCustomers.Commands.Update;
using Application.Features.IndividualCustomers.Profiles;
using BankingSystemCore.Repositories;
using Domain.Entities;

namespace Application.UnitTests.Features.IndividualCustomers;

public class IndividualCustomerMapperTests
{
    [Fact]
    public void ToEntity_CopiesCommandValues_AndLeavesSystemFieldsEmpty()
    {
        var command = new CreateIndividualCustomerCommand
        {
            FirstName = "Ali",
            LastName = "Veli",
            NationalId = "10000000146",
            DateOfBirth = new DateOnly(1990, 1, 1),
            EmploymentStatus = EmploymentStatus.Retired,
            MonthlyIncome = 1234.56m
        };

        IndividualCustomer entity = IndividualCustomerMapper.ToEntity(command);

        entity.FirstName.ShouldBe("Ali");
        entity.DateOfBirth.ShouldBe(new DateOnly(1990, 1, 1));
        entity.EmploymentStatus.ShouldBe(EmploymentStatus.Retired);
        entity.MonthlyIncome.ShouldBe(1234.56m);
        entity.Id.ShouldBe(Guid.Empty);
        entity.CustomerNumber.ShouldBeEmpty();
        entity.CreateDate.ShouldBe(default);
    }

    [Fact]
    public void Apply_UpdatesEntity_WithoutTouchingSystemManagedFields()
    {
        var created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var entity = new IndividualCustomer
        {
            CustomerNumber = "IND-ABC",
            CreditScore = 700,
            CreateDate = created,
            FirstName = "Ali"
        };

        IndividualCustomerMapper.Apply(
            new UpdateIndividualCustomerCommand { Id = Guid.NewGuid(), FirstName = "Ayse", IsActive = true }, entity);

        entity.FirstName.ShouldBe("Ayse");
        entity.IsActive.ShouldBeTrue();
        entity.CustomerNumber.ShouldBe("IND-ABC");
        entity.CreditScore.ShouldBe(700);
        entity.CreateDate.ShouldBe(created);
    }

    [Fact]
    public void ToListResponse_MapsPagingInformationAndItems()
    {
        var page = new Paginate<IndividualCustomer>
        {
            Index = 1,
            Size = 5,
            Count = 12,
            Pages = 3,
            Items = [new IndividualCustomer { FirstName = "Ali", CustomerNumber = "IND-1" }]
        };

        var result = IndividualCustomerMapper.ToListResponse(page);

        result.Index.ShouldBe(1);
        result.Size.ShouldBe(5);
        result.Count.ShouldBe(12);
        result.Pages.ShouldBe(3);
        result.HasNext.ShouldBeTrue();
        result.HasPrevious.ShouldBeTrue();
        result.Items.Count.ShouldBe(1);
        result.Items[0].FirstName.ShouldBe("Ali");
        result.Items[0].CustomerNumber.ShouldBe("IND-1");
    }
}
