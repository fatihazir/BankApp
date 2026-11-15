using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using BankingSystemCore.Security.CurrentUser;
using MediatR;

namespace BankingSystemCore.Application.Pipelines.Authorization;

/// <summary>
/// MediatR pipeline behavior for every <see cref="ISecuredRequest"/>: the caller must be authenticated (401)
/// and, when the request names roles, must have at least one of them (403). Register it before validation.
/// The constraint is on <see cref="ISecuredRequest"/> only: commands without a result implement the non-generic
/// <c>IRequest</c>, which is not <c>IRequest&lt;Unit&gt;</c> in MediatR 12, and must be protected as well.
/// </summary>
public class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, ISecuredRequest
{
    private readonly ICurrentUser _currentUser;

    public AuthorizationBehavior(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
            throw new AuthenticationException("Authentication is required.");

        if (request.Roles.Count > 0 && !request.Roles.Any(_currentUser.IsInRole))
            throw new AuthorizationException("You are not authorized to perform this operation.");

        return await next();
    }
}
