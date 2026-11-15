using Application.Features.LoanApplications.Commands.Create;
using Application.Features.LoanApplications.Profiles;
using Application.Features.LoanTypes.Profiles;
using Application.Features.LoanTypes.Queries.GetList;
using Application.UnitTests.Support;
using BankingSystemCore.Repositories;
using Domain.Entities;

namespace Application.UnitTests.Features.LoanApplications;

public class LoanApplicationMapperTests
{
    private static readonly string[] Navigations = [nameof(LoanApplication.Customer), nameof(LoanApplication.LoanSubType)];

    private static readonly string[] SystemManaged =
    [
        nameof(LoanApplication.Id), nameof(LoanApplication.ApplicationNumber), nameof(LoanApplication.AnnualInterestRate),
        nameof(LoanApplication.Status), nameof(LoanApplication.DecisionDate), nameof(LoanApplication.DecisionReason),
        nameof(LoanApplication.ApprovedAmount), nameof(LoanApplication.CreateDate), nameof(LoanApplication.UpdateDate),
        nameof(LoanApplication.DeleteDate), nameof(LoanApplication.Customer), nameof(LoanApplication.LoanSubType)
    ];

    private static LoanApplication FullApplication()
    {
        var application = MappingContract.Filled<LoanApplication>(true, Navigations);
        application.Customer = new IndividualCustomer { CustomerNumber = "IND-1" };
        application.LoanSubType = new LoanSubType
        {
            Name = "Personal Loan",
            LoanType = new LoanType { Name = "Individual Loan", Segment = CustomerSegment.Individual }
        };
        return application;
    }

    [Fact]
    public void ToEntity_CopiesEveryCommandProperty()
    {
        var command = MappingContract.Filled<CreateLoanApplicationCommand>();

        LoanApplication entity = LoanApplicationMapper.ToEntity(command);

        MappingContract.AssertAllSourcePropertiesCopied(command, entity);
    }

    [Fact]
    public void ToEntity_LeavesSystemManagedFieldsUntouched()
    {
        LoanApplication entity = LoanApplicationMapper.ToEntity(MappingContract.Filled<CreateLoanApplicationCommand>());

        entity.Id.ShouldBe(Guid.Empty);
        entity.ApplicationNumber.ShouldBeEmpty();
        entity.AnnualInterestRate.ShouldBe(0);
        entity.Status.ShouldBe(default);
        entity.DecisionDate.ShouldBeNull();
        entity.ApprovedAmount.ShouldBeNull();
        entity.CreateDate.ShouldBe(default);
    }

    [Fact]
    public void Create_HasEveryEntityPropertyAccountedFor() =>
        MappingContract.AssertEntityPropertiesAreAccountedFor(typeof(LoanApplication), typeof(CreateLoanApplicationCommand), SystemManaged);

    [Fact]
    public void CreatedResponse_CopiesEveryPropertyItHas()
    {
        LoanApplication entity = FullApplication();

        var response = LoanApplicationMapper.ToCreatedResponse(entity);

        // Monthly payment and totals are calculated by the handler, not copied from the entity.
        string[] calculated = [nameof(response.MonthlyPayment), nameof(response.TotalRepayment), nameof(response.TotalInterest)];
        foreach (var property in typeof(CreatedLoanApplicationResponse).GetProperties().Where(p => !calculated.Contains(p.Name)))
            typeof(LoanApplication).GetProperty(property.Name).ShouldNotBeNull($"{property.Name} has no source on the entity.");

        MappingContract.AssertAllSourcePropertiesCopied(response, entity, calculated);
        response.MonthlyPayment.ShouldBe(0m);
    }

    [Fact]
    public void StatusResponse_CopiesEveryPropertyItHas()
    {
        LoanApplication entity = FullApplication();

        var response = LoanApplicationMapper.ToStatusResponse(entity);

        MappingContract.AssertAllTargetPropertiesHaveASource(typeof(LoanApplication), response.GetType());
        MappingContract.AssertAllSourcePropertiesCopied(response, entity);
    }

    [Fact]
    public void GetByIdResponse_CopiesOwnFields_AndFlattensCustomerAndLoanTypeNames()
    {
        LoanApplication entity = FullApplication();

        var response = LoanApplicationMapper.ToGetByIdResponse(entity);

        response.CustomerNumber.ShouldBe("IND-1");
        response.LoanSubTypeName.ShouldBe("Personal Loan");
        response.LoanTypeName.ShouldBe("Individual Loan");
        response.Segment.ShouldBe(CustomerSegment.Individual);

        string[] flattened =
        [
            nameof(response.CustomerNumber), nameof(response.CustomerName), nameof(response.LoanSubTypeName), nameof(response.LoanTypeName), nameof(response.Segment),
            nameof(response.MonthlyPayment), nameof(response.TotalRepayment), nameof(response.TotalInterest)
        ];
        MappingContract.AssertAllSourcePropertiesCopied(response, entity, flattened);
    }

    [Fact]
    public void GetByIdResponse_WithoutIncludedNavigations_DoesNotThrow()
    {
        var response = LoanApplicationMapper.ToGetByIdResponse(new LoanApplication { ApplicationNumber = "LA-1" });

        response.ApplicationNumber.ShouldBe("LA-1");
        response.LoanTypeName.ShouldBeEmpty();
    }

    [Fact]
    public void ListResponse_MapsItemsAndPaging()
    {
        var page = new Paginate<LoanApplication>
        {
            Index = 1, Size = 2, Count = 5, Pages = 3, Items = [FullApplication(), FullApplication()]
        };

        var result = LoanApplicationMapper.ToListResponse(page);

        (result.Index, result.Size, result.Count, result.Pages).ShouldBe((1, 2, 5, 3));
        result.Items.Count.ShouldBe(2);
        result.Items[0].LoanSubTypeName.ShouldBe("Personal Loan");
        result.Items[0].ApplicationNumber.ShouldBe(page.Items[0].ApplicationNumber);
        result.Items[0].Status.ShouldBe(page.Items[0].Status);
    }

    [Fact]
    public void LoanTypeList_CopiesTypeAndEverySubTypeField()
    {
        var subType = MappingContract.Filled<LoanSubType>(true, nameof(LoanSubType.LoanType));
        var loanType = MappingContract.Filled<LoanType>(true, nameof(LoanType.SubTypes));
        loanType.SubTypes = [subType];

        GetListLoanTypeListItemDto dto = LoanTypeMapper.ToListItem(loanType);

        dto.Name.ShouldBe(loanType.Name);
        dto.Segment.ShouldBe(loanType.Segment);
        dto.SubTypes.Count.ShouldBe(1);

        string[] unexposed = typeof(LoanSubType).GetProperties().Select(p => p.Name)
            .Where(n => typeof(LoanSubTypeDto).GetProperty(n) is null).ToArray();
        MappingContract.AssertAllSourcePropertiesCopied(subType, dto.SubTypes[0], unexposed);
        MappingContract.AssertAllTargetPropertiesHaveASource(typeof(LoanSubType), typeof(LoanSubTypeDto));
    }
}
