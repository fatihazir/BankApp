using Application.Features.Users.Queries.GetList;
using BankingSystemCore.Repositories;
using Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace Application.Features.Users.Profiles;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class UserMapper
{
    // For customers these come from the customer part of the user; the handler fills them in.
    [MapperIgnoreTarget(nameof(GetListUserListItemDto.CustomerId))]
    [MapperIgnoreTarget(nameof(GetListUserListItemDto.CustomerNumber))]
    public static partial GetListUserListItemDto ToListItem(ApplicationUser entity);

    public static partial Paginate<GetListUserListItemDto> ToListResponse(Paginate<ApplicationUser> page);
}
