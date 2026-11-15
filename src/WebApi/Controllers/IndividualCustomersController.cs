using Asp.Versioning;
using Application.Features.IndividualCustomers.Commands.Create;
using Application.Features.IndividualCustomers.Commands.Delete;
using Application.Features.IndividualCustomers.Commands.Update;
using Application.Features.IndividualCustomers.Queries.GetById;
using Application.Features.IndividualCustomers.Queries.GetList;
using BankingSystemCore.Repositories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Authorize]
[ApiVersion("0")]
[Route("api/v{version:apiVersion}/individual-customers")]
public class IndividualCustomersController : ControllerBase
{
    private readonly IMediator _mediator;

    public IndividualCustomersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [ProducesResponseType<CreatedIndividualCustomerResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateIndividualCustomerCommand command, CancellationToken cancellationToken)
    {
        CreatedIndividualCustomerResponse response = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<GetByIdIndividualCustomerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        GetByIdIndividualCustomerResponse response =
            await _mediator.Send(new GetByIdIndividualCustomerQuery { Id = id }, cancellationToken);
        return Ok(response);
    }

    [HttpGet]
    [ProducesResponseType<Paginate<GetListIndividualCustomerListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetList([FromQuery] GetListIndividualCustomerQuery query, CancellationToken cancellationToken)
    {
        Paginate<GetListIndividualCustomerListItemDto> response = await _mediator.Send(query, cancellationToken);
        return Ok(response);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<UpdatedIndividualCustomerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateIndividualCustomerCommand command, CancellationToken cancellationToken)
    {
        // The route is the single source of truth for the id.
        command.Id = id;
        UpdatedIndividualCustomerResponse response = await _mediator.Send(command, cancellationToken);
        return Ok(response);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType<DeletedIndividualCustomerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        DeletedIndividualCustomerResponse response =
            await _mediator.Send(new DeleteIndividualCustomerCommand { Id = id }, cancellationToken);
        return Ok(response);
    }
}
