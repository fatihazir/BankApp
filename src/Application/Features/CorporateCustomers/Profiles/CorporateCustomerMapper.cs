using Application.Features.CorporateCustomers.Commands.Create;
using Application.Features.CorporateCustomers.Commands.Delete;
using Application.Features.CorporateCustomers.Commands.Update;
using Application.Features.CorporateCustomers.Queries.GetById;
using Application.Features.CorporateCustomers.Queries.GetList;
using BankingSystemCore.Repositories;
using Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace Application.Features.CorporateCustomers.Profiles;

/// <summary>
/// Compile-time (source generated) mappings between CorporateCustomer entity, commands, queries and responses.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class CorporateCustomerMapper
{
    [MapperIgnoreTarget(nameof(CorporateCustomer.Id))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.CustomerNumber))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.CreditScore))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.IsActive))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.CreateDate))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.UpdateDate))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.DeleteDate))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.PasswordHash))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.FailedLoginCount))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.LockoutEnd))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.LastLoginDate))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.EmailVerified))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.EmailVerifiedDate))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.EmailVerificationTokenHash))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.EmailVerificationTokenExpiresAt))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.RowVersion))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.Role))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.BannedDate))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.BanReason))]
    public static partial CorporateCustomer ToEntity(CreateCorporateCustomerCommand command);

    public static partial CreatedCorporateCustomerResponse ToCreatedResponse(CorporateCustomer entity);

    /// <summary>Copies the command values onto an existing, tracked entity.</summary>
    [MapperIgnoreTarget(nameof(CorporateCustomer.Id))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.CustomerNumber))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.CreditScore))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.CreateDate))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.UpdateDate))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.DeleteDate))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.PasswordHash))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.FailedLoginCount))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.LockoutEnd))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.LastLoginDate))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.EmailVerified))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.EmailVerifiedDate))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.EmailVerificationTokenHash))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.EmailVerificationTokenExpiresAt))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.RowVersion))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.Role))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.BannedDate))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.BanReason))]
    [MapperIgnoreTarget(nameof(CorporateCustomer.Email))]
    public static partial void Apply(UpdateCorporateCustomerCommand command, CorporateCustomer entity);

    public static partial UpdatedCorporateCustomerResponse ToUpdatedResponse(CorporateCustomer entity);

    public static partial DeletedCorporateCustomerResponse ToDeletedResponse(CorporateCustomer entity);

    public static partial GetByIdCorporateCustomerResponse ToGetByIdResponse(CorporateCustomer entity);

    public static partial GetListCorporateCustomerListItemDto ToListItem(CorporateCustomer entity);

    public static partial Paginate<GetListCorporateCustomerListItemDto> ToListResponse(Paginate<CorporateCustomer> page);
}
