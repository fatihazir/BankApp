using Application.Services.CustomerNaming;
using Application.Services.Repositories;
using Application.Services.Security;
using BankingSystemCore.Application.Pipelines.Authorization;
using BankingSystemCore.Security.CurrentUser;
using Domain.Entities;
using MediatR;

namespace Application.Features.Auth.Queries.Me;

/// <summary>Who am I? Returns what the current token says, plus the customer name for customer users.</summary>
public class MeQuery : IRequest<MeResponse>, ISecuredRequest
{
    IReadOnlyCollection<string> ISecuredRequest.Roles => BankRoles.AnyAuthenticated;
}

public class MeQueryHandler : IRequestHandler<MeQuery, MeResponse>
{
    private readonly ICurrentUser _currentUser;
    private readonly CustomerAccessPolicy _access;
    private readonly ICustomerRepository _customers;
    private readonly IApplicationUserRepository _users;

    public MeQueryHandler(
        ICurrentUser currentUser, CustomerAccessPolicy access, ICustomerRepository customers, IApplicationUserRepository users)
    {
        _currentUser = currentUser;
        _access = access;
        _customers = customers;
        _users = users;
    }

    public async Task<MeResponse> Handle(MeQuery request, CancellationToken cancellationToken)
    {
        string? customerName = null;
        if (_access.OwnCustomerId is { } customerId)
        {
            Customer? customer = await _customers.GetAsync(
                c => c.Id == customerId, enableTracking: false, cancellationToken: cancellationToken);
            customerName = CustomerDisplayName.Of(customer);
        }

        Guid? userId = _currentUser.Id;
        bool emailVerified = userId is not null
            && (await _users.GetAsync(u => u.Id == userId, enableTracking: false, cancellationToken: cancellationToken))?.EmailVerified == true;

        return new MeResponse
        {
            EmailVerified = emailVerified,
            UserId = _currentUser.Id,
            Email = _currentUser.Email ?? string.Empty,
            Roles = _currentUser.Roles.ToList(),
            CustomerId = _access.OwnCustomerId,
            CustomerName = customerName
        };
    }
}
