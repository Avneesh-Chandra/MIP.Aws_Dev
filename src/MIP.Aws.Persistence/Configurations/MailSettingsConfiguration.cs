using MIP.Aws.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MIP.Aws.Persistence.Configurations;

public sealed class MailSettingsConfiguration : IEntityTypeConfiguration<MailSettings>
{
    public void Configure(EntityTypeBuilder<MailSettings> builder)
    {
        builder.ToTable("MailSettings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ActiveProvider).HasMaxLength(64).IsRequired();
        builder.Property(x => x.RedirectAllTo).HasMaxLength(320);
        builder.Property(x => x.SubjectPrefix).HasMaxLength(128).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
