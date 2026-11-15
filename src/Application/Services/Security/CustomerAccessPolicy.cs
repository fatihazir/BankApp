using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using BankingSystemCore.Security.CurrentUser;

namespace Application.Services.Security;

/// <summary>
/// Decides whose customer data the current user may touch: bank staff everyone's, a customer only their own.
/// The roles on a request say who may run it; this policy says whose data it may be about.
/// </summary>
public class CustomerAccessPolicy
{
    private readonly ICurrentUser _currentUser;

    public CustomerAccessPolicy(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public bool IsStaff => _currentUser.IsInRole(BankRoles.BankStaff);

    /// <summary>
    /// The customer record of a customer user. A customer is a user, so the customer id is the user id (the "sub"
    /// claim). Null for staff.
    /// </summary>
    public Guid? OwnCustomerId => _currentUser.IsInRole(BankRoles.Customer) ? _currentUser.Id : null;

    public bool CanAccess(Guid customerId) => IsStaff || (OwnCustomerId is { } own && own == customerId);

    /// <summary>For requests that name a customer: acting for somebody else is forbidden (403).</summary>
    public void EnsureCanAccess(Guid customerId)
    {
        if (!CanAccess(customerId))
            throw new AuthorizationException("You can only access your own data.");
    }
}
