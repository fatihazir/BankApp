using Application.Features.Auth.Rules;
using Application.Services.Repositories;
using Application.Services.Security;
using BankingSystemCore.Application.Pipelines.Authorization;
using BankingSystemCore.Security.Hashing;
using Domain.Entities;
using MediatR;

namespace Application.Features.Users.Commands.CreateStaff;

/// <summary>Bank staff create accounts for new bank staff.</summary>
public class CreateStaffUserCommand : IRequest<CreatedStaffUserResponse>, ISecuredRequest
{
    IReadOnlyCollection<string> ISecuredRequest.Roles => BankRoles.StaffOnly;

    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class CreateStaffUserCommandHandler : IRequestHandler<CreateStaffUserCommand, CreatedStaffUserResponse>
{
    private readonly IApplicationUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly AuthBusinessRules _rules;
    private readonly TimeProvider _time;

    public CreateStaffUserCommandHandler(IApplicationUserRepository users, IPasswordHasher hasher, AuthBusinessRules rules, TimeProvider time)
    {
        _users = users;
        _hasher = hasher;
        _rules = rules;
        _time = time;
    }

    public async Task<CreatedStaffUserResponse> Handle(CreateStaffUserCommand request, CancellationToken cancellationToken)
    {
        string email = AuthBusinessRules.NormalizeEmail(request.Email);
        await _rules.EmailCannotBeRegisteredTwice(email, cancellationToken);

        var user = new ApplicationUser
        {
            Email = email, PasswordHash = _hasher.Hash(request.Password), Role = UserRole.BankStaff, IsActive = true,
            // Created by authorized staff, so the address is trusted.
            EmailVerified = true, EmailVerifiedDate = _time.GetUtcNow().UtcDateTime
        };
        await _users.AddAsync(user, cancellationToken);

        return new CreatedStaffUserResponse { UserId = user.Id, Email = user.Email, Role = user.Role };
    }
}
