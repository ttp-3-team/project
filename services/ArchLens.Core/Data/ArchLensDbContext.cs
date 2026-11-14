using ArchLens.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ArchLens.Core.Data;

/// <summary>EF Core context. Snake-case names; snapshots stored as <c>jsonb</c> through the canonical JSON settings.</summary>
public sealed class ArchLensDbContext(DbContextOptions<ArchLensDbContext> options) : DbContext(options)
{
    public DbSet<WorkspaceRecord> Workspaces => Set<WorkspaceRecord>();
    public DbSet<ModelRecord> Models => Set<ModelRecord>();
    public DbSet<VersionRecord> Versions => Set<VersionRecord>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<WorkspaceRecord>().HasIndex(x => x.OwnerId);
        b.Entity<ModelRecord>().HasOne(x => x.Workspace).WithMany().HasForeignKey(x => x.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<VersionRecord>(e =>
        {
            e.HasIndex(x => new { x.ModelId, x.Number }).IsUnique();
            e.HasOne(x => x.Model).WithMany().HasForeignKey(x => x.ModelId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<VersionRecord>().WithMany().HasForeignKey(x => x.ParentVersionId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Snapshot)
                .HasColumnType("jsonb")
                .HasConversion(
                    s => SnapshotJson.Serialize(s),
                    json => SnapshotJson.Parse(json).Value,
                    new ValueComparer<ModelSnapshot>((a, c) => ReferenceEquals(a, c), s => s.GetHashCode(), s => s));
        });
    }
}
