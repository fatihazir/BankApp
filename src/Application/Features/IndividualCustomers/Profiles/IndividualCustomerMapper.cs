using Application.Features.IndividualCustomers.Commands.Create;
using Application.Features.IndividualCustomers.Commands.Delete;
using Application.Features.IndividualCustomers.Commands.Update;
using Application.Features.IndividualCustomers.Queries.GetById;
using Application.Features.IndividualCustomers.Queries.GetList;
using BankingSystemCore.Repositories;
using Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace Application.Features.IndividualCustomers.Profiles;

/// <summary>
/// Compile-time (source generated) mappings between IndividualCustomer entity, commands, queries and responses.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class IndividualCustomerMapper
{
    [MapperIgnoreTarget(nameof(IndividualCustomer.Id))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.CustomerNumber))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.CreditScore))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.IsActive))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.CreateDate))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.UpdateDate))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.DeleteDate))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.PasswordHash))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.FailedLoginCount))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.LockoutEnd))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.LastLoginDate))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.EmailVerified))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.EmailVerifiedDate))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.EmailVerificationTokenHash))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.EmailVerificationTokenExpiresAt))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.RowVersion))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.Role))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.BannedDate))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.BanReason))]
    public static partial IndividualCustomer ToEntity(CreateIndividualCustomerCommand command);

    public static partial CreatedIndividualCustomerResponse ToCreatedResponse(IndividualCustomer entity);

    /// <summary>Copies the command values onto an existing, tracked entity.</summary>
    [MapperIgnoreTarget(nameof(IndividualCustomer.Id))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.CustomerNumber))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.CreditScore))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.CreateDate))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.UpdateDate))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.DeleteDate))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.PasswordHash))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.FailedLoginCount))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.LockoutEnd))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.LastLoginDate))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.EmailVerified))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.EmailVerifiedDate))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.EmailVerificationTokenHash))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.EmailVerificationTokenExpiresAt))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.RowVersion))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.Role))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.BannedDate))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.BanReason))]
    [MapperIgnoreTarget(nameof(IndividualCustomer.Email))]
    public static partial void Apply(UpdateIndividualCustomerCommand command, IndividualCustomer entity);

    public static partial UpdatedIndividualCustomerResponse ToUpdatedResponse(IndividualCustomer entity);

    public static partial DeletedIndividualCustomerResponse ToDeletedResponse(IndividualCustomer entity);

    public static partial GetByIdIndividualCustomerResponse ToGetByIdResponse(IndividualCustomer entity);

    public static partial GetListIndividualCustomerListItemDto ToListItem(IndividualCustomer entity);

    public static partial Paginate<GetListIndividualCustomerListItemDto> ToListResponse(Paginate<IndividualCustomer> page);
}
