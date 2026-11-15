using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.EntityConfigurations;

public class LoanTypeConfiguration : IEntityTypeConfiguration<LoanType>
{
    public void Configure(EntityTypeBuilder<LoanType> builder)
    {
        builder.ToTable("LoanTypes");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).IsRequired().HasMaxLength(100);
        builder.Property(t => t.Description).HasMaxLength(500);
        builder.Property(t => t.Segment).IsRequired().HasConversion<int>();
        builder.Property(t => t.CreateDate).IsRequired();

        builder.HasIndex(t => t.Name).IsUnique();

        // A type cannot be deleted while sub types still point at it.
        builder.HasMany(t => t.SubTypes)
            .WithOne(s => s.LoanType)
            .HasForeignKey(s => s.LoanTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(LoanSeedData.LoanTypes());
    }
}
