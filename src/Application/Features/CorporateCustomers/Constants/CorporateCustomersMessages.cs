namespace Application.Features.CorporateCustomers.Constants;

public static class CorporateCustomersMessages
{
    public const string CustomerNotFound = "Corporate customer was not found.";
    public const string TaxNumberInvalid = "Tax number must be 10 digits.";
    public const string TaxNumberAlreadyExists = "A corporate customer with this tax number already exists.";
    public const string FoundationDateInFuture = "Foundation date cannot be in the future.";
}
