using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertySearch.Domain;

namespace PropertySearch.Infrastructure.Configurations;

internal sealed class ListingConfiguration : IEntityTypeConfiguration<Listing>
{
    public void Configure(EntityTypeBuilder<Listing> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.ExternalId).IsRequired();
        builder.Property(l => l.Url).IsRequired();
        builder.Property(l => l.DisplayAddress).IsRequired();

        builder.Property(l => l.RentPcm).HasColumnType("numeric(10,2)");
        builder.Property(l => l.Bedrooms).IsRequired();

        builder.Property(l => l.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(l => l.FirstSeenAt).IsRequired();
        builder.Property(l => l.LastSeenAt).IsRequired();

        // Dedup key: a portal listing must not be ingested twice.
        builder.HasIndex(l => new { l.SourceId, l.ExternalId }).IsUnique();

        builder.HasOne(l => l.Source)
            .WithMany(s => s.Listings)
            .HasForeignKey(l => l.SourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Property)
            .WithMany(p => p.Listings)
            .HasForeignKey(l => l.PropertyId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.ConfigureAuditing();
    }
}
