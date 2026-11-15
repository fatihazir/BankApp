using Application.Services.Security;
using BankingSystemCore.Application.Pipelines.Authorization;
using Application.Features.CorporateCustomers.Profiles;
using Application.Features.CorporateCustomers.Rules;
using Application.Features.Auth.Rules;
using Application.Services.Repositories;
using BankingSystemCore.Security.Hashing;
using Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.CorporateCustomers.Commands.Create;

public class CreateCorporateCustomerCommand : IRequest<CreatedCorporateCustomerResponse>, ISecuredRequest
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

public class CreateCorporateCustomerCommandHandler
    : IRequestHandler<CreateCorporateCustomerCommand, CreatedCorporateCustomerResponse>
{
    private readonly ICorporateCustomerRepository _repository;
    private readonly CorporateCustomerBusinessRules _rules;
    private readonly AuthBusinessRules _authRules;
    private readonly IPasswordHasher _hasher;
    private readonly EmailVerificationService _verification;
    private readonly ILogger<CreateCorporateCustomerCommandHandler> _logger;

    public CreateCorporateCustomerCommandHandler(
        ICorporateCustomerRepository repository,
        CorporateCustomerBusinessRules rules,
        AuthBusinessRules authRules,
        IPasswordHasher hasher,
        EmailVerificationService verification,
        ILogger<CreateCorporateCustomerCommandHandler> logger)
    {
        _repository = repository;
        _rules = rules;
        _authRules = authRules;
        _hasher = hasher;
        _verification = verification;
        _logger = logger;
    }

    public async Task<CreatedCorporateCustomerResponse> Handle(
        CreateCorporateCustomerCommand request, CancellationToken cancellationToken)
    {
        _rules.TaxNumberMustBeValid(request.TaxNumber);
        _rules.FoundationDateCannotBeInFuture(request.FoundationDate);
        await _rules.TaxNumberCannotBeDuplicated(request.TaxNumber, cancellationToken: cancellationToken);

        // The e-mail address is the login name, so it must be free among all users (staff and customers).
        string email = AuthBusinessRules.NormalizeEmail(request.Email);
        await _authRules.EmailCannotBeRegisteredTwice(email, cancellationToken);

        CorporateCustomer customer = CorporateCustomerMapper.ToEntity(request);

        // Login part (Users): who the person is for the system.
        customer.Email = email;
        customer.PasswordHash = _hasher.Hash(request.Password);
        customer.IsActive = true;
        // Application part (ApplicationUsers): the role is always Customer, set by the entity itself. A caller can
        // never choose a role; staff accounts are created only by the staff endpoint.
        // Customer part (Customers) and type part:
        customer.CustomerNumber = "COR-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

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

        return CorporateCustomerMapper.ToCreatedResponse(customer);
    }
}
