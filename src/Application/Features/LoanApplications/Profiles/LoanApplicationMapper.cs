using Application.Features.LoanApplications.Commands;
using Application.Features.LoanApplications.Commands.Create;
using Application.Features.LoanApplications.Queries.GetById;
using Application.Features.LoanApplications.Queries.GetList;
using BankingSystemCore.Repositories;
using Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace Application.Features.LoanApplications.Profiles;

/// <summary>
/// Compile-time (source generated) mappings between LoanApplication, its commands and responses.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class LoanApplicationMapper
{
    [MapperIgnoreTarget(nameof(LoanApplication.Id))]
    [MapperIgnoreTarget(nameof(LoanApplication.ApplicationNumber))]
    [MapperIgnoreTarget(nameof(LoanApplication.AnnualInterestRate))]
    [MapperIgnoreTarget(nameof(LoanApplication.Status))]
    [MapperIgnoreTarget(nameof(LoanApplication.DecisionDate))]
    [MapperIgnoreTarget(nameof(LoanApplication.DecisionReason))]
    [MapperIgnoreTarget(nameof(LoanApplication.ApprovedAmount))]
    [MapperIgnoreTarget(nameof(LoanApplication.CreateDate))]
    [MapperIgnoreTarget(nameof(LoanApplication.UpdateDate))]
    [MapperIgnoreTarget(nameof(LoanApplication.DeleteDate))]
    [MapperIgnoreTarget(nameof(LoanApplication.Customer))]
    [MapperIgnoreTarget(nameof(LoanApplication.LoanSubType))]
    public static partial LoanApplication ToEntity(CreateLoanApplicationCommand command);

    [MapperIgnoreTarget(nameof(CreatedLoanApplicationResponse.MonthlyPayment))]
    [MapperIgnoreTarget(nameof(CreatedLoanApplicationResponse.TotalRepayment))]
    [MapperIgnoreTarget(nameof(CreatedLoanApplicationResponse.TotalInterest))]
    public static partial CreatedLoanApplicationResponse ToCreatedResponse(LoanApplication entity);

    public static partial LoanApplicationStatusResponse ToStatusResponse(LoanApplication entity);

    [MapperIgnoreTarget(nameof(GetByIdLoanApplicationResponse.CustomerName))]
    [MapProperty("Customer.CustomerNumber", nameof(GetByIdLoanApplicationResponse.CustomerNumber))]
    [MapProperty("LoanSubType.Name", nameof(GetByIdLoanApplicationResponse.LoanSubTypeName))]
    [MapProperty("LoanSubType.LoanType.Name", nameof(GetByIdLoanApplicationResponse.LoanTypeName))]
    [MapProperty("LoanSubType.LoanType.Segment", nameof(GetByIdLoanApplicationResponse.Segment))]
    [MapperIgnoreTarget(nameof(GetByIdLoanApplicationResponse.MonthlyPayment))]
    [MapperIgnoreTarget(nameof(GetByIdLoanApplicationResponse.TotalRepayment))]
    [MapperIgnoreTarget(nameof(GetByIdLoanApplicationResponse.TotalInterest))]
    public static partial GetByIdLoanApplicationResponse ToGetByIdResponse(LoanApplication entity);

    [MapperIgnoreTarget(nameof(GetListLoanApplicationListItemDto.CustomerName))]
    [MapProperty("Customer.CustomerNumber", nameof(GetListLoanApplicationListItemDto.CustomerNumber))]
    [MapProperty("LoanSubType.Name", nameof(GetListLoanApplicationListItemDto.LoanSubTypeName))]
    [MapProperty("LoanSubType.LoanType.Name", nameof(GetListLoanApplicationListItemDto.LoanTypeName))]
    [MapProperty("LoanSubType.LoanType.Segment", nameof(GetListLoanApplicationListItemDto.Segment))]
    public static partial GetListLoanApplicationListItemDto ToListItem(LoanApplication entity);

    public static partial Paginate<GetListLoanApplicationListItemDto> ToListResponse(Paginate<LoanApplication> page);
}
