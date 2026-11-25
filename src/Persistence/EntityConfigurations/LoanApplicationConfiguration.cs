using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.EntityConfigurations;

public class LoanApplicationConfiguration : IEntityTypeConfiguration<LoanApplication>
{
    public void Configure(EntityTypeBuilder<LoanApplication> builder)
    {
        builder.ToTable("LoanApplications");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.ApplicationNumber).IsRequired().HasMaxLength(30);
        builder.Property(a => a.RequestedAmount).HasPrecision(18, 2);
        builder.Property(a => a.ApprovedAmount).HasPrecision(18, 2);
        builder.Property(a => a.AnnualInterestRate).HasPrecision(5, 2);
        builder.Property(a => a.Purpose).HasMaxLength(500);
        builder.Property(a => a.DecisionReason).HasMaxLength(1000);
        builder.Property(a => a.Status).IsRequired().HasConversion<int>();
        builder.Property(a => a.CreateDate).IsRequired();
        builder.Property(a => a.RowVersion).IsRowVersion();

        builder.HasIndex(a => a.ApplicationNumber).IsUnique();
        builder.HasIndex(a => a.CustomerId);
        builder.HasIndex(a => a.LoanSubTypeId);
        builder.HasIndex(a => a.Status);

        // One foreign key to the shared Customers table serves both individual and corporate customers.
        builder.HasOne(a => a.Customer)
            .WithMany()
            .HasForeignKey(a => a.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.LoanSubType)
            .WithMany()
            .HasForeignKey(a => a.LoanSubTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
