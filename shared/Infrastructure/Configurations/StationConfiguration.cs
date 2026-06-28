using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertySearch.Domain;

namespace PropertySearch.Infrastructure.Configurations;

internal sealed class StationConfiguration : IEntityTypeConfiguration<Station>
{
    public void Configure(EntityTypeBuilder<Station> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.StationCode).IsRequired();
        builder.HasIndex(s => s.StationCode).IsUnique();

        builder.Property(s => s.Name).IsRequired();

        builder.Property(s => s.Mode)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(s => s.Latitude).IsRequired();
        builder.Property(s => s.Longitude).IsRequired();

        builder.ConfigureAuditing();
    }
}
