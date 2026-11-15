namespace Application.Services.Security;

/// <summary>Role names used in tokens and in <c>ISecuredRequest.Roles</c>. They equal the names of <c>UserRole</c>.</summary>
public static class BankRoles
{
    public const string Customer = "Customer";
    public const string BankStaff = "BankStaff";

    public static readonly string[] StaffOnly = [BankStaff];
    public static readonly string[] StaffAndCustomer = [BankStaff, Customer];

    /// <summary>Any logged-in user, whatever the role.</summary>
    public static readonly string[] AnyAuthenticated = [];
}
