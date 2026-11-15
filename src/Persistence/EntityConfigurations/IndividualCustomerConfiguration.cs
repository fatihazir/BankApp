using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.EntityConfigurations;

public class IndividualCustomerConfiguration : IEntityTypeConfiguration<IndividualCustomer>
{
    public void Configure(EntityTypeBuilder<IndividualCustomer> builder)
    {
        builder.ToTable("IndividualCustomers");

        builder.Property(c => c.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(c => c.LastName).IsRequired().HasMaxLength(100);
        builder.Property(c => c.NationalId).IsRequired().HasMaxLength(11).IsFixedLength();
        builder.Property(c => c.MotherName).HasMaxLength(100);
        builder.Property(c => c.EmploymentStatus).IsRequired().HasConversion<int>();
        builder.Property(c => c.Occupation).HasMaxLength(100);
        builder.Property(c => c.EmployerName).HasMaxLength(200);
        builder.Property(c => c.MonthlyIncome).HasPrecision(18, 2);

        builder.HasIndex(c => c.NationalId).IsUnique();
    }
}
