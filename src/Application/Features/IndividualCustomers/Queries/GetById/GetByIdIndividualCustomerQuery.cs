using Application.Features.IndividualCustomers.Constants;
using Application.Services.Security;
using BankingSystemCore.Application.Pipelines.Authorization;
using Application.Features.IndividualCustomers.Profiles;
using Application.Features.IndividualCustomers.Rules;
using Application.Services.Repositories;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using Domain.Entities;
using MediatR;

namespace Application.Features.IndividualCustomers.Queries.GetById;

public class GetByIdIndividualCustomerQuery : IRequest<GetByIdIndividualCustomerResponse>, ISecuredRequest
{
    IReadOnlyCollection<string> ISecuredRequest.Roles => BankRoles.StaffAndCustomer;

    public Guid Id { get; set; }
}

public class GetByIdIndividualCustomerQueryHandler
    : IRequestHandler<GetByIdIndividualCustomerQuery, GetByIdIndividualCustomerResponse>
{
    private readonly IIndividualCustomerRepository _repository;
    private readonly IndividualCustomerBusinessRules _rules;
    private readonly CustomerAccessPolicy _access;

    public GetByIdIndividualCustomerQueryHandler(
        IIndividualCustomerRepository repository,
        IndividualCustomerBusinessRules rules,
        CustomerAccessPolicy access)
    {
        _repository = repository;
        _rules = rules;
        _access = access;
    }

    public async Task<GetByIdIndividualCustomerResponse> Handle(
        GetByIdIndividualCustomerQuery request, CancellationToken cancellationToken)
    {
        // A customer can only reach their own record; for others it looks like it does not exist.
        if (!_access.CanAccess(request.Id))
            throw new NotFoundException(IndividualCustomersMessages.CustomerNotFound);

        IndividualCustomer? customer = await _repository.GetAsync(
            c => c.Id == request.Id, enableTracking: false, cancellationToken: cancellationToken);
        _rules.CustomerShouldExist(customer);

        return IndividualCustomerMapper.ToGetByIdResponse(customer!);
    }
}
