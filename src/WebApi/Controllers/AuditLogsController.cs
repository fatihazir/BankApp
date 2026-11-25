using Asp.Versioning;
using Application.Features.AuditLogs.Queries.GetList;
using BankingSystemCore.Repositories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>The audit trail: who did what, to what, when and from where. Read only. Bank staff only.</summary>
[ApiController]
[Authorize]
[ApiVersion("0")]
[Route("api/v{version:apiVersion}/audit-logs")]
public class AuditLogsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuditLogsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lists audit entries, newest first. Filters: <c>actorUserId</c>, <c>targetId</c>, <c>action</c> (prefix, e.g.
    /// <c>LoanApplication</c> or <c>Auth.Login</c>), <c>succeeded</c>, <c>from</c>, <c>to</c> (UTC).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<Paginate<GetListAuditLogListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetList([FromQuery] GetListAuditLogQuery query, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(query, cancellationToken));
}
