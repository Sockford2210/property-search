using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertySearch.Domain;

namespace PropertySearch.Infrastructure.Configurations;

internal sealed class PropertyConfiguration : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.RentPcm).HasColumnType("numeric(10,2)");

        builder.ConfigureAuditing();
    }
}
