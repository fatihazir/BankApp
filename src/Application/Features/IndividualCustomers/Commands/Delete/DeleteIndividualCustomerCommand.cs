using BankingSystemCore.Application.Pipelines.Auditing;
using Application.Services.Security;
using BankingSystemCore.Application.Pipelines.Authorization;
using Application.Features.IndividualCustomers.Profiles;
using Application.Features.IndividualCustomers.Rules;
using Application.Services.Repositories;
using Domain.Entities;
using MediatR;

namespace Application.Features.IndividualCustomers.Commands.Delete;

[Audit("Customer.Individual.Delete", "IndividualCustomer")]
public class DeleteIndividualCustomerCommand : IRequest<DeletedIndividualCustomerResponse>, ISecuredRequest
{
    IReadOnlyCollection<string> ISecuredRequest.Roles => BankRoles.StaffOnly;

    public Guid Id { get; set; }
}

public class DeleteIndividualCustomerCommandHandler
    : IRequestHandler<DeleteIndividualCustomerCommand, DeletedIndividualCustomerResponse>
{
    private readonly IIndividualCustomerRepository _repository;
    private readonly IndividualCustomerBusinessRules _rules;
    private readonly UserSessionService _sessions;

    public DeleteIndividualCustomerCommandHandler(
        IIndividualCustomerRepository repository,
        IndividualCustomerBusinessRules rules,
        UserSessionService sessions)
    {
        _repository = repository;
        _rules = rules;
        _sessions = sessions;
    }

    public async Task<DeletedIndividualCustomerResponse> Handle(
        DeleteIndividualCustomerCommand request, CancellationToken cancellationToken)
    {
        IndividualCustomer? customer = await _repository.GetAsync(
            c => c.Id == request.Id, cancellationToken: cancellationToken);
        _rules.CustomerShouldExist(customer);

        // Deleted customers cannot log in, and tokens they already hold stop working immediately.
        customer!.IsActive = false;
        await _repository.DeleteAsync(customer, cancellationToken: cancellationToken);
        await _sessions.RevokeAllAsync(customer.Id, "Customer deleted", cancellationToken);

        return IndividualCustomerMapper.ToDeletedResponse(customer!);
    }
}
