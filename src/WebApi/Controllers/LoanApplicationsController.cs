using Asp.Versioning;
using Application.Features.LoanApplications.Commands;
using Application.Features.LoanApplications.Commands.Approve;
using Application.Features.LoanApplications.Commands.Cancel;
using Application.Features.LoanApplications.Commands.Create;
using Application.Features.LoanApplications.Commands.Reject;
using Application.Features.LoanApplications.Commands.StartReview;
using Application.Features.LoanApplications.Queries.Calculate;
using Application.Features.LoanApplications.Queries.GetById;
using Application.Features.LoanApplications.Queries.GetList;
using BankingSystemCore.Repositories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Authorize]
[ApiVersion("0")]
[Route("api/v{version:apiVersion}/loan-applications")]
public class LoanApplicationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public LoanApplicationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>A customer applies for a loan. Individual customers can only use individual loan types, corporate customers only corporate ones.</summary>
    [HttpPost]
    [ProducesResponseType<CreatedLoanApplicationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateLoanApplicationCommand command, CancellationToken cancellationToken)
    {
        CreatedLoanApplicationResponse response = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<GetByIdLoanApplicationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        GetByIdLoanApplicationResponse response =
            await _mediator.Send(new GetByIdLoanApplicationQuery { Id = id }, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Calculates monthly payment, total repayment and the installment schedule for a loan sub type,
    /// without creating an application. Uses the current interest rate of the sub type.
    /// </summary>
    [HttpGet("calculate")]
    [ProducesResponseType<CalculateLoanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Calculate([FromQuery] CalculateLoanQuery query, CancellationToken cancellationToken)
    {
        CalculateLoanResponse response = await _mediator.Send(query, cancellationToken);
        return Ok(response);
    }

    /// <summary>Lists applications, newest first. Optional filters: <c>customerId</c>, <c>status</c>.</summary>
    [HttpGet]
    [ProducesResponseType<Paginate<GetListLoanApplicationListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetList([FromQuery] GetListLoanApplicationQuery query, CancellationToken cancellationToken)
    {
        Paginate<GetListLoanApplicationListItemDto> response = await _mediator.Send(query, cancellationToken);
        return Ok(response);
    }

    /// <summary>Submitted -> UnderReview.</summary>
    [HttpPost("{id:guid}/start-review")]
    [ProducesResponseType<LoanApplicationStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StartReview(Guid id, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new StartReviewLoanApplicationCommand { Id = id }, cancellationToken));

    /// <summary>UnderReview -> Approved. The approved amount cannot exceed the requested amount.</summary>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType<LoanApplicationStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveLoanApplicationCommand command, CancellationToken cancellationToken)
    {
        command.Id = id;
        return Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>Submitted or UnderReview -> Rejected. A reason is required.</summary>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType<LoanApplicationStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectLoanApplicationCommand command, CancellationToken cancellationToken)
    {
        command.Id = id;
        return Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>Submitted or UnderReview -> Cancelled. The body (reason) is optional.</summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<LoanApplicationStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(
        Guid id, [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] CancelLoanApplicationCommand? command,
        CancellationToken cancellationToken)
    {
        command ??= new CancelLoanApplicationCommand();
        command.Id = id;
        return Ok(await _mediator.Send(command, cancellationToken));
    }
}
