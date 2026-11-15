using BankingSystemCore.Security.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.EntityConfigurations;

/// <summary>
/// Root of the user hierarchy. Table-per-type: Users (login), ApplicationUsers (role, ban), Customers, and
/// IndividualCustomers / CorporateCustomers, so one insert of a customer writes a row in each of them.
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.UseTptMappingStrategy();
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).IsRequired().HasMaxLength(256);
        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(500);
        builder.Property(u => u.EmailVerificationTokenHash).HasMaxLength(64).IsFixedLength();
        builder.Property(u => u.RowVersion).IsRowVersion();
        builder.Property(u => u.CreateDate).IsRequired();

        // The e-mail address is the login name of every user, staff and customers alike.
        builder.HasIndex(u => u.Email).IsUnique();
    }
}
