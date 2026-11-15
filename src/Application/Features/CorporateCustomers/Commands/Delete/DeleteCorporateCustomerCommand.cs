using Application.Services.Security;
using BankingSystemCore.Application.Pipelines.Authorization;
using Application.Features.CorporateCustomers.Profiles;
using Application.Features.CorporateCustomers.Rules;
using Application.Services.Repositories;
using Domain.Entities;
using MediatR;

namespace Application.Features.CorporateCustomers.Commands.Delete;

public class DeleteCorporateCustomerCommand : IRequest<DeletedCorporateCustomerResponse>, ISecuredRequest
{
    IReadOnlyCollection<string> ISecuredRequest.Roles => BankRoles.StaffOnly;

    public Guid Id { get; set; }
}

public class DeleteCorporateCustomerCommandHandler
    : IRequestHandler<DeleteCorporateCustomerCommand, DeletedCorporateCustomerResponse>
{
    private readonly ICorporateCustomerRepository _repository;
    private readonly CorporateCustomerBusinessRules _rules;
    private readonly UserSessionService _sessions;

    public DeleteCorporateCustomerCommandHandler(
        ICorporateCustomerRepository repository,
        CorporateCustomerBusinessRules rules,
        UserSessionService sessions)
    {
        _repository = repository;
        _rules = rules;
        _sessions = sessions;
    }

    public async Task<DeletedCorporateCustomerResponse> Handle(
        DeleteCorporateCustomerCommand request, CancellationToken cancellationToken)
    {
        CorporateCustomer? customer = await _repository.GetAsync(
            c => c.Id == request.Id, cancellationToken: cancellationToken);
        _rules.CustomerShouldExist(customer);

        // Deleted customers cannot log in, and tokens they already hold stop working immediately.
        customer!.IsActive = false;
        await _repository.DeleteAsync(customer, cancellationToken: cancellationToken);
        await _sessions.RevokeAllAsync(customer.Id, "Customer deleted", cancellationToken);

        return CorporateCustomerMapper.ToDeletedResponse(customer!);
    }
}
