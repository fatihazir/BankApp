using System.Diagnostics;
using System.Reflection;
using BankingSystemCore.Security.CurrentUser;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BankingSystemCore.Application.Pipelines.Auditing;

/// <summary>
/// Writes an audit entry after a request marked with <see cref="AuditAttribute"/> succeeded. Requests without the
/// attribute pass through untouched, and a request that fails writes nothing (it changed nothing).
/// The change is already saved when the entry is written, so a failing audit store must not turn a
/// completed action into an error: it is logged loudly with the whole entry instead.
/// </summary>
public class AuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const int MaxDetailLength = 500;

    private static readonly AuditAttribute? Marker = typeof(TRequest).GetCustomAttribute<AuditAttribute>();
    private static readonly PropertyInfo? RequestId = IdProperty(typeof(TRequest));
    private static readonly PropertyInfo? ResponseId = IdProperty(typeof(TResponse));

    private readonly IAuditWriter _writer;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<AuditBehavior<TRequest, TResponse>> _logger;

    public AuditBehavior(IAuditWriter writer, ICurrentUser currentUser, ILogger<AuditBehavior<TRequest, TResponse>> logger)
    {
        _writer = writer;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        TResponse response = await next(cancellationToken);

        if (Marker is null)
            return response;

        AuditEntry entry = BuildEntry(request, response);
        try
        {
            await _writer.WriteAsync(entry, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogCritical(exception,
                "The audit entry could not be written. Action {Action}, actor {ActorUserId}, target {TargetType} {TargetId}, detail {Detail}.",
                entry.Action, entry.ActorUserId, entry.TargetType, entry.TargetId, entry.Detail);
        }

        return response;
    }

    private AuditEntry BuildEntry(TRequest request, TResponse response)
    {
        string? detail = (request as IAuditDetail)?.AuditDetail;
        if (detail is { Length: > MaxDetailLength })
            detail = detail[..MaxDetailLength];

        return new AuditEntry
        {
            ActorUserId = _currentUser.Id,
            ActorEmail = _currentUser.Email,
            ActorRole = _currentUser.Roles.FirstOrDefault(),
            Action = Marker!.Action,
            TargetType = Marker.TargetType,
            TargetId = ReadId(RequestId, request) ?? (response as IAuditTarget)?.AuditTargetId ?? ReadId(ResponseId, response),
            Detail = detail,
            IpAddress = _currentUser.IpAddress,
            TraceId = Activity.Current?.TraceId.ToString()
        };
    }

    private static PropertyInfo? IdProperty(Type type) =>
        type.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance, binder: null, returnType: typeof(Guid), types: Type.EmptyTypes, modifiers: null);

    private static Guid? ReadId(PropertyInfo? property, object? instance)
    {
        if (property is null || instance is null)
            return null;
        var id = (Guid)property.GetValue(instance)!;
        return id == Guid.Empty ? null : id;
    }
}
