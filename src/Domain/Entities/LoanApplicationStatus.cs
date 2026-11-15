namespace Domain.Entities;

public enum LoanApplicationStatus
{
    /// <summary>Received from the customer, not looked at yet.</summary>
    Submitted = 1,

    /// <summary>Being evaluated by the bank.</summary>
    UnderReview = 2,

    Approved = 3,

    Rejected = 4,

    /// <summary>Withdrawn before a decision was made.</summary>
    Cancelled = 5
}
