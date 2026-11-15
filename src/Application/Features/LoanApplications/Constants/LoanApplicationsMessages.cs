namespace Application.Features.LoanApplications.Constants;

public static class LoanApplicationsMessages
{
    public const string ApplicationNotFound = "Loan application was not found.";
    public const string CustomerNotFound = "Customer was not found.";
    public const string CustomerInactive = "An inactive customer cannot apply for a loan.";
    public const string LoanSubTypeNotFound = "Loan sub type was not found.";
    public const string LoanSubTypeInactive = "This loan sub type is not available for applications.";
    public const string SegmentMismatch = "This customer cannot apply for this loan type. Individual customers can only apply for individual loans and corporate customers only for corporate loans.";
    public const string AmountOutOfRange = "Requested amount must be between {0} and {1} for this loan type.";
    public const string TermOutOfRange = "Term must be between {0} and {1} months for this loan type.";
    public const string OpenApplicationExists = "The customer already has an open application for this loan sub type.";
    public const string InvalidStatus = "This operation is not allowed while the application is {0}.";
    public const string ApprovedAmountInvalid = "Approved amount must be greater than zero and cannot exceed the requested amount.";
}
