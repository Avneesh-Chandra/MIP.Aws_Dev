using MIP.Aws.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MIP.Aws.Persistence.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(x => x.DisplayName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.EntraObjectId).HasMaxLength(64);
        builder.HasIndex(x => x.EntraObjectId).IsUnique().HasFilter("[EntraObjectId] IS NOT NULL");
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.Property(x => x.Description).HasMaxLength(512);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ReplacedByTokenHash).HasMaxLength(128);
        builder.Property(x => x.CreatedByIp).HasMaxLength(64);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.TokenHash);
        builder.HasOne(x => x.User).WithMany(x => x.RefreshTokens).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class UserAuditLogConfiguration : IEntityTypeConfiguration<UserAuditLog>
{
    public void Configure(EntityTypeBuilder<UserAuditLog> builder)
    {
        builder.ToTable("UserAuditLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Details).HasMaxLength(4000);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.HasIndex(x => x.OccurredAt);
    }
}
