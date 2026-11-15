using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.EntityConfigurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.ActorEmail).HasMaxLength(256);
        builder.Property(l => l.ActorRole).HasMaxLength(50);
        builder.Property(l => l.Action).IsRequired().HasMaxLength(100);
        builder.Property(l => l.TargetType).HasMaxLength(100);
        builder.Property(l => l.Detail).HasMaxLength(500);
        builder.Property(l => l.IpAddress).HasMaxLength(45);
        builder.Property(l => l.TraceId).HasMaxLength(64);
        builder.Property(l => l.CreateDate).IsRequired();

        // No foreign keys: the trail must outlive the users and customers it talks about.
        builder.HasIndex(l => l.CreateDate);
        builder.HasIndex(l => l.ActorUserId);
        builder.HasIndex(l => l.TargetId);
        builder.HasIndex(l => l.Action);
    }
}
