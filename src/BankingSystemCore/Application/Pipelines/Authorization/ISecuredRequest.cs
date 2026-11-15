namespace BankingSystemCore.Application.Pipelines.Authorization;

/// <summary>
/// Marks a command or query that needs a logged-in user. <see cref="AuthorizationBehavior{TRequest, TResponse}"/>
/// checks it before the handler runs. Requests that do not implement it are open to everyone.
/// </summary>
public interface ISecuredRequest
{
    /// <summary>
    /// Roles allowed to run the request (any one is enough). Empty means any authenticated user.
    /// Implement it explicitly so the property is not part of the request body.
    /// </summary>
    IReadOnlyCollection<string> Roles { get; }
}
