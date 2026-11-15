using Application.Features.Auth.Constants;
using Application.Services.Repositories;
using Application.Services.Security;
using BankingSystemCore.Application.Pipelines.Authorization;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using BankingSystemCore.Security.CurrentUser;
using Domain.Entities;
using MediatR;

namespace Application.Features.Auth.Commands.SendEmailVerification;

/// <summary>The logged-in user asks for a (new) verification e-mail for their own address.</summary>
public class SendEmailVerificationCommand : IRequest<SentEmailVerificationResponse>, ISecuredRequest
{
    IReadOnlyCollection<string> ISecuredRequest.Roles => BankRoles.AnyAuthenticated;
}

public class SendEmailVerificationCommandHandler : IRequestHandler<SendEmailVerificationCommand, SentEmailVerificationResponse>
{
    private readonly ICurrentUser _currentUser;
    private readonly IApplicationUserRepository _users;
    private readonly EmailVerificationService _service;

    public SendEmailVerificationCommandHandler(ICurrentUser currentUser, IApplicationUserRepository users, EmailVerificationService service)
    {
        _currentUser = currentUser;
        _users = users;
        _service = service;
    }

    public async Task<SentEmailVerificationResponse> Handle(SendEmailVerificationCommand request, CancellationToken cancellationToken)
    {
        Guid userId = _currentUser.Id ?? throw new AuthenticationException("Authentication is required.");

        ApplicationUser user = await _users.GetAsync(u => u.Id == userId, cancellationToken: cancellationToken)
            ?? throw new NotFoundException(AuthMessages.UserNotFound);

        if (user.EmailVerified)
            throw new BusinessException(AuthMessages.EmailAlreadyVerified);

        string token = _service.IssueToken(user); // a new token replaces the old one
        await _users.UpdateAsync(user, cancellationToken);
        await _service.SendAsync(user, token, cancellationToken);

        return new SentEmailVerificationResponse { Email = user.Email, ExpiresAt = user.EmailVerificationTokenExpiresAt!.Value };
    }
}
