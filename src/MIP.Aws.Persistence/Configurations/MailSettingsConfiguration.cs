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
        builder.Property(x => x.StatusEmailRecipient).HasMaxLength(320);
        builder.Property(x => x.StatusEmailSelectedRecipients).HasMaxLength(4000);
        builder.Property(x => x.AdminRecipientEmail).HasMaxLength(320);
        builder.Property(x => x.StatusEmailCompletionMode).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.BatchScheduleTimeUtc).HasMaxLength(5);
        builder.Property(x => x.BatchRecurrence).HasMaxLength(32);
        builder.Property(x => x.BatchRecurrenceDays).HasMaxLength(32);
        builder.Property(x => x.PdfDownloadMailRecipient).HasMaxLength(320);
        builder.Property(x => x.PdfDownloadMailSelectedRecipients).HasMaxLength(4000);
        builder.Property(x => x.PdfDownloadMailCompletionMode).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.PdfDownloadMailSubject).HasMaxLength(256);
        builder.Property(x => x.PdfDownloadMailTimeUtc).HasMaxLength(5);
        builder.Property(x => x.PdfDownloadMailDesign).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
