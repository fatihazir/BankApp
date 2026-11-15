using BankingSystemCore.Application.Pipelines.Auditing;
using Application.Services.Security;
using BankingSystemCore.Application.Pipelines.Authorization;
using Application.Features.IndividualCustomers.Profiles;
using Application.Features.IndividualCustomers.Rules;
using Application.Features.Auth.Rules;
using Application.Services.Repositories;
using BankingSystemCore.Security.Hashing;
using Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.IndividualCustomers.Commands.Create;

[Audit("Customer.Individual.Create", "IndividualCustomer")]
public class CreateIndividualCustomerCommand : IRequest<CreatedIndividualCustomerResponse>, ISecuredRequest
{
    IReadOnlyCollection<string> ISecuredRequest.Roles => BankRoles.StaffOnly;

    /// <summary>The login name of the new customer. It must not be used by any other user.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>The initial password of the customer's online account. Never stored as is.</summary>
    public string Password { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? PostalCode { get; set; }

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

public class CreateIndividualCustomerCommandHandler
    : IRequestHandler<CreateIndividualCustomerCommand, CreatedIndividualCustomerResponse>
{
    private readonly IIndividualCustomerRepository _repository;
    private readonly IndividualCustomerBusinessRules _rules;
    private readonly AuthBusinessRules _authRules;
    private readonly IPasswordHasher _hasher;
    private readonly EmailVerificationService _verification;
    private readonly ILogger<CreateIndividualCustomerCommandHandler> _logger;

    public CreateIndividualCustomerCommandHandler(
        IIndividualCustomerRepository repository,
        IndividualCustomerBusinessRules rules,
        AuthBusinessRules authRules,
        IPasswordHasher hasher,
        EmailVerificationService verification,
        ILogger<CreateIndividualCustomerCommandHandler> logger)
    {
        _repository = repository;
        _rules = rules;
        _authRules = authRules;
        _hasher = hasher;
        _verification = verification;
        _logger = logger;
    }

    public async Task<CreatedIndividualCustomerResponse> Handle(
        CreateIndividualCustomerCommand request, CancellationToken cancellationToken)
    {
        _rules.NationalIdMustBeValid(request.NationalId);
        _rules.CustomerMustBeAdult(request.DateOfBirth);
        await _rules.NationalIdCannotBeDuplicated(request.NationalId, cancellationToken: cancellationToken);

        // The e-mail address is the login name, so it must be free among all users (staff and customers).
        string email = AuthBusinessRules.NormalizeEmail(request.Email);
        await _authRules.EmailCannotBeRegisteredTwice(email, cancellationToken);

        IndividualCustomer customer = IndividualCustomerMapper.ToEntity(request);

        // Login part (Users): who the person is for the system.
        customer.Email = email;
        customer.PasswordHash = _hasher.Hash(request.Password);
        customer.IsActive = true;
        // Application part (ApplicationUsers): the role is always Customer, set by the entity itself. A caller can
        // never choose a role; staff accounts are created only by the staff endpoint.
        // Customer part (Customers) and type part:
        customer.CustomerNumber = "IND-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

        string verificationToken = _verification.IssueToken(customer);

        // One save, one transaction: either the rows of every table exist, or none of them.
        await _repository.AddAsync(customer, cancellationToken);

        // The account exists now; a failing mail server must not undo it (the e-mail can be requested again).
        try
        {
            await _verification.SendAsync(customer, verificationToken, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "The verification e-mail for customer {CustomerId} could not be sent.", customer.Id);
        }

        return IndividualCustomerMapper.ToCreatedResponse(customer);
    }
}
