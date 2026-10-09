using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class ClaimDocumentConfiguration : IEntityTypeConfiguration<ClaimDocument>
{
    public void Configure(EntityTypeBuilder<ClaimDocument> builder)
    {
        builder.ToTable("ClaimDocuments");

        builder.Property(d => d.OriginalFileName).IsRequired().HasMaxLength(255);
        builder.Property(d => d.SanitizedFileName).IsRequired().HasMaxLength(255);
        builder.Property(d => d.ContentType).IsRequired().HasMaxLength(255);
        builder.Property(d => d.StoragePath).IsRequired().HasMaxLength(1024);
        builder.Property(d => d.DocumentType).HasMaxLength(100);

        builder.HasIndex(d => d.ClaimId);

        builder.HasOne<Claim>()
            .WithMany()
            .HasForeignKey(d => d.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
