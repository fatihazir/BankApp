using BankingSystemCore.Application.Pipelines.Auditing;
using Application.Features.IndividualCustomers.Constants;
using Application.Services.Security;
using BankingSystemCore.Application.Pipelines.Authorization;
using Application.Features.IndividualCustomers.Profiles;
using Application.Features.IndividualCustomers.Rules;
using Application.Services.Repositories;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using Domain.Entities;
using MediatR;

namespace Application.Features.IndividualCustomers.Commands.Update;

[Audit("Customer.Individual.Update", "IndividualCustomer")]
public class UpdateIndividualCustomerCommand : IRequest<UpdatedIndividualCustomerResponse>, ISecuredRequest
{
    IReadOnlyCollection<string> ISecuredRequest.Roles => BankRoles.StaffAndCustomer;

    public Guid Id { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public bool IsActive { get; set; }

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string? MotherName { get; set; }
    public EmploymentStatus EmploymentStatus { get; set; }
    public string? Occupation { get; set; }
    public string? EmployerName { get; set; }
    public decimal MonthlyIncome { get; set; }
}

public class UpdateIndividualCustomerCommandHandler
    : IRequestHandler<UpdateIndividualCustomerCommand, UpdatedIndividualCustomerResponse>
{
    private readonly IIndividualCustomerRepository _repository;
    private readonly IndividualCustomerBusinessRules _rules;
    private readonly CustomerAccessPolicy _access;

    public UpdateIndividualCustomerCommandHandler(
        IIndividualCustomerRepository repository,
        IndividualCustomerBusinessRules rules,
        CustomerAccessPolicy access)
    {
        _repository = repository;
        _rules = rules;
        _access = access;
    }

    public async Task<UpdatedIndividualCustomerResponse> Handle(
        UpdateIndividualCustomerCommand request, CancellationToken cancellationToken)
    {
        // A customer can only reach their own record; for others it looks like it does not exist.
        if (!_access.CanAccess(request.Id))
            throw new NotFoundException(IndividualCustomersMessages.CustomerNotFound);

        IndividualCustomer? customer = await _repository.GetAsync(
            c => c.Id == request.Id, cancellationToken: cancellationToken);
        _rules.CustomerShouldExist(customer);

        _rules.NationalIdMustBeValid(request.NationalId);
        _rules.CustomerMustBeAdult(request.DateOfBirth);
        await _rules.NationalIdCannotBeDuplicated(request.NationalId, request.Id, cancellationToken);

        IndividualCustomerMapper.Apply(request, customer!);
        await _repository.UpdateAsync(customer!, cancellationToken);

        return IndividualCustomerMapper.ToUpdatedResponse(customer!);
    }
}
