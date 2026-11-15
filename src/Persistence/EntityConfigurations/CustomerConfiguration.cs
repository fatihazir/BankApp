using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.EntityConfigurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.Property(c => c.CustomerNumber).IsRequired().HasMaxLength(20);
        builder.Property(c => c.PhoneNumber).IsRequired().HasMaxLength(20);
        builder.Property(c => c.Address).IsRequired().HasMaxLength(500);
        builder.Property(c => c.City).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Country).IsRequired().HasMaxLength(100);
        builder.Property(c => c.PostalCode).HasMaxLength(10);

        builder.HasIndex(c => c.CustomerNumber).IsUnique();
    }
}
