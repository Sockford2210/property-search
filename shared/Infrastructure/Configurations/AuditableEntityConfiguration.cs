using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertySearch.Domain;

namespace PropertySearch.Infrastructure.Configurations;

internal static class AuditableEntityConfiguration
{
    /// <summary>
    /// Maps the shared audit columns: <c>created_at</c> is database-generated on
    /// insert (defaults to <c>now()</c>); <c>updated_at</c> is written by the
    /// DbContext on every save.
    /// </summary>
    public static void ConfigureAuditing<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : AuditableEntity
    {
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()");
    }
}
