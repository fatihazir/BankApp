namespace Application.Services.Security;

/// <summary>
/// Asked on every request that carries a token: is the session of this token still alive? It is what makes
/// a ban or a logout work immediately, although the JWT itself is valid until it expires.
/// </summary>
public interface ISessionValidator
{
    Task<bool> IsActiveAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
