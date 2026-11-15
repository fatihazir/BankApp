using Application.Features.CorporateCustomers.Constants;
using Application.Services.Security;
using BankingSystemCore.Application.Pipelines.Authorization;
using Application.Features.CorporateCustomers.Profiles;
using Application.Features.CorporateCustomers.Rules;
using Application.Services.Repositories;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using Domain.Entities;
using MediatR;

namespace Application.Features.CorporateCustomers.Queries.GetById;

public class GetByIdCorporateCustomerQuery : IRequest<GetByIdCorporateCustomerResponse>, ISecuredRequest
{
    IReadOnlyCollection<string> ISecuredRequest.Roles => BankRoles.StaffAndCustomer;

    public Guid Id { get; set; }
}

public class GetByIdCorporateCustomerQueryHandler
    : IRequestHandler<GetByIdCorporateCustomerQuery, GetByIdCorporateCustomerResponse>
{
    private readonly ICorporateCustomerRepository _repository;
    private readonly CorporateCustomerBusinessRules _rules;
    private readonly CustomerAccessPolicy _access;

    public GetByIdCorporateCustomerQueryHandler(
        ICorporateCustomerRepository repository,
        CorporateCustomerBusinessRules rules,
        CustomerAccessPolicy access)
    {
        _repository = repository;
        _rules = rules;
        _access = access;
    }

    public async Task<GetByIdCorporateCustomerResponse> Handle(
        GetByIdCorporateCustomerQuery request, CancellationToken cancellationToken)
    {
        // A customer can only reach their own record; for others it looks like it does not exist.
        if (!_access.CanAccess(request.Id))
            throw new NotFoundException(CorporateCustomersMessages.CustomerNotFound);

        CorporateCustomer? customer = await _repository.GetAsync(
            c => c.Id == request.Id, enableTracking: false, cancellationToken: cancellationToken);
        _rules.CustomerShouldExist(customer);

        return CorporateCustomerMapper.ToGetByIdResponse(customer!);
    }
}
