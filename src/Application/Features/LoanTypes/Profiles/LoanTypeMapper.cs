using Application.Features.LoanTypes.Commands.Create;
using Application.Features.LoanTypes.Queries.GetList;
using BankingSystemCore.Repositories;
using Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace Application.Features.LoanTypes.Profiles;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class LoanTypeMapper
{
    [MapperIgnoreTarget(nameof(LoanType.Id))]
    [MapperIgnoreTarget(nameof(LoanType.IsActive))]
    [MapperIgnoreTarget(nameof(LoanType.SubTypes))]
    [MapperIgnoreTarget(nameof(LoanType.CreateDate))]
    [MapperIgnoreTarget(nameof(LoanType.UpdateDate))]
    [MapperIgnoreTarget(nameof(LoanType.DeleteDate))]
    public static partial LoanType ToEntity(CreateLoanTypeCommand command);

    public static partial CreatedLoanTypeResponse ToCreatedResponse(LoanType entity);

    public static partial GetListLoanTypeListItemDto ToListItem(LoanType entity);

    public static partial LoanSubTypeDto ToSubTypeDto(LoanSubType entity);

    public static partial Paginate<GetListLoanTypeListItemDto> ToListResponse(Paginate<LoanType> page);
}
