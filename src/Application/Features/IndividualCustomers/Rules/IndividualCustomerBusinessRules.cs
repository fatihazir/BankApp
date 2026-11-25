using Application.Features.IndividualCustomers.Constants;
using Application.Services.Repositories;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using Domain.Entities;

namespace Application.Features.IndividualCustomers.Rules;

public class IndividualCustomerBusinessRules
{
    private const int MinimumAge = 18;

    private readonly IIndividualCustomerRepository _repository;
    private readonly TimeProvider _time;

    public IndividualCustomerBusinessRules(IIndividualCustomerRepository repository, TimeProvider time)
    {
        _repository = repository;
        _time = time;
    }

    public void CustomerShouldExist(IndividualCustomer? customer)
    {
        if (customer is null)
            throw new NotFoundException(IndividualCustomersMessages.CustomerNotFound);
    }

    public void NationalIdMustBeValid(string nationalId)
    {
        if (!IsValidNationalId(nationalId))
            throw new BusinessException(IndividualCustomersMessages.NationalIdInvalid);
    }

    public async Task NationalIdCannotBeDuplicated(string nationalId, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        // Soft-deleted rows are included because the database unique index covers them too.
        bool exists = await _repository.AnyAsync(
            c => c.NationalId == nationalId && (excludeId == null || c.Id != excludeId),
            withDeleted: true,
            cancellationToken: cancellationToken);

        if (exists)
            throw new BusinessException(IndividualCustomersMessages.NationalIdAlreadyExists);
    }

    public void CustomerMustBeAdult(DateOnly dateOfBirth)
    {
        DateOnly today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);
        if (dateOfBirth > today.AddYears(-MinimumAge))
            throw new BusinessException(IndividualCustomersMessages.CustomerMustBeAdult);
    }

    private static bool IsValidNationalId(string nationalId)
    {
        if (nationalId.Length != 11 || !nationalId.All(char.IsAsciiDigit) || nationalId[0] == '0')
            return false;

        int[] d = nationalId.Select(c => c - '0').ToArray();
        int odd = d[0] + d[2] + d[4] + d[6] + d[8];
        int even = d[1] + d[3] + d[5] + d[7];
        int tenth = ((odd * 7 - even) % 10 + 10) % 10;
        int eleventh = d.Take(10).Sum() % 10;

        return d[9] == tenth && d[10] == eleventh;
    }
}
