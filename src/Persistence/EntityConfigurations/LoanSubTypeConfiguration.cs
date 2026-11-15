using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.EntityConfigurations;

public class LoanSubTypeConfiguration : IEntityTypeConfiguration<LoanSubType>
{
    public void Configure(EntityTypeBuilder<LoanSubType> builder)
    {
        builder.ToTable("LoanSubTypes");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).IsRequired().HasMaxLength(100);
        builder.Property(s => s.Description).HasMaxLength(500);
        builder.Property(s => s.MinAmount).HasPrecision(18, 2);
        builder.Property(s => s.MaxAmount).HasPrecision(18, 2);
        builder.Property(s => s.AnnualInterestRate).HasPrecision(5, 2);
        builder.Property(s => s.CreateDate).IsRequired();

        // Names are unique within one loan type.
        builder.HasIndex(s => new { s.LoanTypeId, s.Name }).IsUnique();

        builder.HasData(LoanSeedData.LoanSubTypes());
    }
}
