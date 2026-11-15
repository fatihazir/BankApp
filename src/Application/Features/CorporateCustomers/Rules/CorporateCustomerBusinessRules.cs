using Application.Features.CorporateCustomers.Constants;
using Application.Services.Repositories;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using Domain.Entities;

namespace Application.Features.CorporateCustomers.Rules;

public class CorporateCustomerBusinessRules
{
    private readonly ICorporateCustomerRepository _repository;

    public CorporateCustomerBusinessRules(ICorporateCustomerRepository repository)
    {
        _repository = repository;
    }

    public void CustomerShouldExist(CorporateCustomer? customer)
    {
        if (customer is null)
            throw new NotFoundException(CorporateCustomersMessages.CustomerNotFound);
    }

    public void TaxNumberMustBeValid(string taxNumber)
    {
        if (taxNumber.Length != 10 || !taxNumber.All(char.IsAsciiDigit))
            throw new BusinessException(CorporateCustomersMessages.TaxNumberInvalid);
    }

    public async Task TaxNumberCannotBeDuplicated(string taxNumber, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        // Soft-deleted rows are included because the database unique index covers them too.
        bool exists = await _repository.AnyAsync(
            c => c.TaxNumber == taxNumber && (excludeId == null || c.Id != excludeId),
            withDeleted: true,
            cancellationToken: cancellationToken);

        if (exists)
            throw new BusinessException(CorporateCustomersMessages.TaxNumberAlreadyExists);
    }

    public void FoundationDateCannotBeInFuture(DateOnly foundationDate)
    {
        if (foundationDate > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new BusinessException(CorporateCustomersMessages.FoundationDateInFuture);
    }
}
