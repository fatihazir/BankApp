using Application.Features.CorporateCustomers.Constants;
using Application.Services.Security;
using BankingSystemCore.Application.Pipelines.Authorization;
using Application.Features.CorporateCustomers.Profiles;
using Application.Features.CorporateCustomers.Rules;
using Application.Services.Repositories;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using Domain.Entities;
using MediatR;

namespace Application.Features.CorporateCustomers.Commands.Update;

public class UpdateCorporateCustomerCommand : IRequest<UpdatedCorporateCustomerResponse>, ISecuredRequest
{
    IReadOnlyCollection<string> ISecuredRequest.Roles => BankRoles.StaffAndCustomer;

    public Guid Id { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public bool IsActive { get; set; }

    public string CompanyName { get; set; } = string.Empty;
    public string TaxNumber { get; set; } = string.Empty;
    public string TaxOffice { get; set; } = string.Empty;
    public string? TradeRegistryNumber { get; set; }
    public DateOnly FoundationDate { get; set; }
    public string? Sector { get; set; }
    public int EmployeeCount { get; set; }
    public decimal AnnualRevenue { get; set; }
    public string AuthorizedPersonName { get; set; } = string.Empty;
    public string? AuthorizedPersonTitle { get; set; }
    public string? AuthorizedPersonNationalId { get; set; }
}

public class UpdateCorporateCustomerCommandHandler
    : IRequestHandler<UpdateCorporateCustomerCommand, UpdatedCorporateCustomerResponse>
{
    private readonly ICorporateCustomerRepository _repository;
    private readonly CorporateCustomerBusinessRules _rules;
    private readonly CustomerAccessPolicy _access;

    public UpdateCorporateCustomerCommandHandler(
        ICorporateCustomerRepository repository,
        CorporateCustomerBusinessRules rules,
        CustomerAccessPolicy access)
    {
        _repository = repository;
        _rules = rules;
        _access = access;
    }

    public async Task<UpdatedCorporateCustomerResponse> Handle(
        UpdateCorporateCustomerCommand request, CancellationToken cancellationToken)
    {
        // A customer can only reach their own record; for others it looks like it does not exist.
        if (!_access.CanAccess(request.Id))
            throw new NotFoundException(CorporateCustomersMessages.CustomerNotFound);

        CorporateCustomer? customer = await _repository.GetAsync(
            c => c.Id == request.Id, cancellationToken: cancellationToken);
        _rules.CustomerShouldExist(customer);

        _rules.TaxNumberMustBeValid(request.TaxNumber);
        _rules.FoundationDateCannotBeInFuture(request.FoundationDate);
        await _rules.TaxNumberCannotBeDuplicated(request.TaxNumber, request.Id, cancellationToken);

        CorporateCustomerMapper.Apply(request, customer!);
        await _repository.UpdateAsync(customer!, cancellationToken);

        return CorporateCustomerMapper.ToUpdatedResponse(customer!);
    }
}
