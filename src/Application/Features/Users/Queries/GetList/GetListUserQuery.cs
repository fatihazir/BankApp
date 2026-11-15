using Application.Features.Users.Profiles;
using Application.Services.Repositories;
using Application.Services.Security;
using BankingSystemCore.Application.Pipelines.Authorization;
using BankingSystemCore.Repositories;
using Domain.Entities;
using MediatR;

namespace Application.Features.Users.Queries.GetList;

/// <summary>Lists the user accounts, newest first. Bank staff only.</summary>
public class GetListUserQuery : IRequest<Paginate<GetListUserListItemDto>>, ISecuredRequest
{
    IReadOnlyCollection<string> ISecuredRequest.Roles => BankRoles.StaffOnly;

    /// <summary>Zero-based page index.</summary>
    public int Index { get; set; } = 0;

    public int Size { get; set; } = 10;

    /// <summary>Optional filter: <c>false</c> shows the banned and deactivated users.</summary>
    public bool? IsActive { get; set; }

    public UserRole? Role { get; set; }
}

public class GetListUserQueryHandler : IRequestHandler<GetListUserQuery, Paginate<GetListUserListItemDto>>
{
    private readonly IApplicationUserRepository _users;

    public GetListUserQueryHandler(IApplicationUserRepository users)
    {
        _users = users;
    }

    public async Task<Paginate<GetListUserListItemDto>> Handle(GetListUserQuery request, CancellationToken cancellationToken)
    {
        bool? isActive = request.IsActive;
        UserRole? role = request.Role;

        Paginate<ApplicationUser> page = await _users.GetListAsync(
            predicate: u => (isActive == null || u.IsActive == isActive) && (role == null || u.Role == role),
            orderBy: q => q.OrderByDescending(u => u.CreateDate),
            index: request.Index,
            size: request.Size,
            enableTracking: false,
            cancellationToken: cancellationToken);

        Paginate<GetListUserListItemDto> response = UserMapper.ToListResponse(page);

        for (int i = 0; i < response.Items.Count; i++)
        {
            // A customer is a user: its customer id is the user id.
            if (page.Items[i] is Customer customer)
            {
                response.Items[i].CustomerId = customer.Id;
                response.Items[i].CustomerNumber = customer.CustomerNumber;
            }
        }

        return response;
    }
}
