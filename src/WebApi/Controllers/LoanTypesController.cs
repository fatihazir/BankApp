using Asp.Versioning;
using Application.Features.LoanSubTypes.Commands.Create;
using Application.Features.LoanTypes.Commands.Create;
using Application.Features.LoanTypes.Queries.GetList;
using BankingSystemCore.Repositories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Authorize]
[ApiVersion("0")]
[Route("api/v{version:apiVersion}/loan-types")]
public class LoanTypesController : ControllerBase
{
    private readonly IMediator _mediator;

    public LoanTypesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Creates a loan type (e.g. a new category for one customer segment).</summary>
    [HttpPost]
    [ProducesResponseType<CreatedLoanTypeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateLoanTypeCommand command, CancellationToken cancellationToken)
    {
        CreatedLoanTypeResponse response = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetList), new { segment = response.Segment }, response);
    }

    /// <summary>Creates a sub type (a concrete loan product with limits and interest rate) under a loan type.</summary>
    [HttpPost("{loanTypeId:guid}/sub-types")]
    [ProducesResponseType<CreatedLoanSubTypeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateSubType(
        Guid loanTypeId, [FromBody] CreateLoanSubTypeCommand command, CancellationToken cancellationToken)
    {
        // The route is the single source of truth for the loan type.
        command.LoanTypeId = loanTypeId;
        CreatedLoanSubTypeResponse response = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetList), new { }, response);
    }

    /// <summary>Lists the active loan types with their active sub types. Paged like every list. Filter with <c>?segment=Individual</c> or <c>Corporate</c>.</summary>
    [HttpGet]
    [ProducesResponseType<Paginate<GetListLoanTypeListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetList([FromQuery] GetListLoanTypeQuery query, CancellationToken cancellationToken)
    {
        Paginate<GetListLoanTypeListItemDto> response = await _mediator.Send(query, cancellationToken);
        return Ok(response);
    }
}
