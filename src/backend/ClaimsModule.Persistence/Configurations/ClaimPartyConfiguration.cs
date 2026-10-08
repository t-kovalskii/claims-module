using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class ClaimPartyConfiguration : IEntityTypeConfiguration<ClaimParty>
{
    public void Configure(EntityTypeBuilder<ClaimParty> builder)
    {
        builder.ToTable("ClaimParties");

        builder.Property(p => p.Role).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.PartyType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.FirstName).HasMaxLength(100);
        builder.Property(p => p.LastName).HasMaxLength(100);
        builder.Property(p => p.CompanyName).HasMaxLength(255);
        builder.Property(p => p.Email).HasMaxLength(255);
        builder.Property(p => p.Phone).HasMaxLength(50);

        builder.HasIndex(p => p.ClaimId);
    }
}
