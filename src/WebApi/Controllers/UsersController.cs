using Application.Features.Users.Commands.Ban;
using Application.Features.Users.Commands.CreateStaff;
using Application.Features.Users.Commands.Unban;
using Application.Features.Users.Queries.GetList;
using BankingSystemCore.Repositories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>User accounts. Bank staff only.</summary>
[ApiController]
[Authorize]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Lists user accounts (staff and customers), newest first. Filters: <c>isActive</c>, <c>role</c>.</summary>
    [HttpGet]
    [ProducesResponseType<Paginate<GetListUserListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetList([FromQuery] GetListUserQuery query, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(query, cancellationToken));

    /// <summary>Creates a bank staff account.</summary>
    [HttpPost("staff")]
    [ProducesResponseType<CreatedStaffUserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateStaff([FromBody] CreateStaffUserCommand command, CancellationToken cancellationToken)
    {
        CreatedStaffUserResponse response = await _mediator.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>
    /// Bans a user. They cannot log in any more and every token they hold stops working immediately
    /// (the next request with it gets 401). You cannot ban yourself.
    /// </summary>
    [HttpPost("{id:guid}/ban")]
    [ProducesResponseType<BannedUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Ban(Guid id, [FromBody] BanUserCommand command, CancellationToken cancellationToken)
    {
        command.Id = id;
        return Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>Lifts a ban. The user has to log in again.</summary>
    [HttpPost("{id:guid}/unban")]
    [ProducesResponseType<UnbannedUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unban(Guid id, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new UnbanUserCommand { Id = id }, cancellationToken));
}
