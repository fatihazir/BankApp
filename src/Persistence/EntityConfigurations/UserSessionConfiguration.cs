using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.EntityConfigurations;

public class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("UserSessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.RefreshTokenHash).IsRequired().HasMaxLength(64).IsFixedLength();
        builder.Property(s => s.PreviousRefreshTokenHash).HasMaxLength(64).IsFixedLength();
        builder.Property(s => s.RevokeReason).HasMaxLength(200);
        builder.Property(s => s.RowVersion).IsRowVersion();
        builder.Property(s => s.CreateDate).IsRequired();

        // Looked up by hash on every refresh.
        builder.HasIndex(s => s.RefreshTokenHash).IsUnique();
        builder.HasIndex(s => s.PreviousRefreshTokenHash);
        builder.HasIndex(s => s.UserId);

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
