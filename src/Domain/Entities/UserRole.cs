namespace Domain.Entities;

public enum UserRole
{
    /// <summary>A bank customer. Can only access their own data.</summary>
    Customer = 1,

    /// <summary>Bank employee. Can access all data.</summary>
    BankStaff = 2
}
