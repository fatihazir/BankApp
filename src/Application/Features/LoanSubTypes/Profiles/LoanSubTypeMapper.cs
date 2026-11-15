using Application.Features.LoanSubTypes.Commands.Create;
using Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace Application.Features.LoanSubTypes.Profiles;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class LoanSubTypeMapper
{
    [MapperIgnoreTarget(nameof(LoanSubType.Id))]
    [MapperIgnoreTarget(nameof(LoanSubType.LoanType))]
    [MapperIgnoreTarget(nameof(LoanSubType.IsActive))]
    [MapperIgnoreTarget(nameof(LoanSubType.CreateDate))]
    [MapperIgnoreTarget(nameof(LoanSubType.UpdateDate))]
    [MapperIgnoreTarget(nameof(LoanSubType.DeleteDate))]
    public static partial LoanSubType ToEntity(CreateLoanSubTypeCommand command);

    public static partial CreatedLoanSubTypeResponse ToCreatedResponse(LoanSubType entity);
}
