using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertySearch.Domain;

namespace PropertySearch.Infrastructure.Configurations;

internal sealed class SourceConfiguration : IEntityTypeConfiguration<Source>
{
    public void Configure(EntityTypeBuilder<Source> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Code).IsRequired();
        builder.HasIndex(s => s.Code).IsUnique();

        builder.Property(s => s.Name).IsRequired();
        builder.Property(s => s.Enabled).IsRequired();

        builder.ConfigureAuditing();
    }
}
