using Microsoft.EntityFrameworkCore;
using PropertySearch.Domain;

namespace PropertySearch.Infrastructure;

public class PropertyDbContext(DbContextOptions<PropertyDbContext> options) : DbContext(options)
{
    public DbSet<Source> Sources => Set<Source>();

    public DbSet<Listing> Listings => Set<Listing>();

    public DbSet<Property> Properties => Set<Property>();

    public DbSet<Station> Stations => Set<Station>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PropertyDbContext).Assembly);
    }

    public override int SaveChanges()
    {
        ApplyAuditTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyAuditTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Maintains <see cref="AuditableEntity.UpdatedAt"/> on every insert and
    /// update. <see cref="AuditableEntity.CreatedAt"/> is left to the database
    /// default on insert and never modified thereafter.
    /// </summary>
    private void ApplyAuditTimestamps()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
