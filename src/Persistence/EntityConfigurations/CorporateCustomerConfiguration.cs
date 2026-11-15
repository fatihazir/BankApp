using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.EntityConfigurations;

public class CorporateCustomerConfiguration : IEntityTypeConfiguration<CorporateCustomer>
{
    public void Configure(EntityTypeBuilder<CorporateCustomer> builder)
    {
        builder.ToTable("CorporateCustomers");

        builder.Property(c => c.CompanyName).IsRequired().HasMaxLength(200);
        builder.Property(c => c.TaxNumber).IsRequired().HasMaxLength(10);
        builder.Property(c => c.TaxOffice).IsRequired().HasMaxLength(100);
        builder.Property(c => c.TradeRegistryNumber).HasMaxLength(50);
        builder.Property(c => c.Sector).HasMaxLength(100);
        builder.Property(c => c.AnnualRevenue).HasPrecision(18, 2);
        builder.Property(c => c.AuthorizedPersonName).IsRequired().HasMaxLength(200);
        builder.Property(c => c.AuthorizedPersonTitle).HasMaxLength(100);
        builder.Property(c => c.AuthorizedPersonNationalId).HasMaxLength(11);

        builder.HasIndex(c => c.TaxNumber).IsUnique();
    }
}
