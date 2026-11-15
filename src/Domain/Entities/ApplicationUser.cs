using BankingSystemCore.Security.Users;

namespace Domain.Entities;

/// <summary>
/// A user of this application: bank staff, or (through <see cref="Customer"/>) a bank customer.
/// Adds what the core <see cref="User"/> does not know: the role and the ban.
/// </summary>
public class ApplicationUser : User
{
    public UserRole Role { get; set; }

    /// <summary>Set when staff ban the user. A banned user is also <see cref="User.IsActive"/> = false.</summary>
    public DateTime? BannedDate { get; set; }

    public string? BanReason { get; set; }
}
