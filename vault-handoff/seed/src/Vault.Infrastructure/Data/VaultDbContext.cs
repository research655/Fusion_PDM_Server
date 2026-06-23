using Microsoft.EntityFrameworkCore;
using Vault.Domain.Entities;

namespace Vault.Infrastructure.Data;

public class VaultDbContext : DbContext
{
    public VaultDbContext(DbContextOptions<VaultDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Repository> Repositories => Set<Repository>();
    public DbSet<VaultFile> Files => Set<VaultFile>();
    public DbSet<FileHistory> History => Set<FileHistory>();
    public DbSet<FileRevisionBackup> RevisionBackups => Set<FileRevisionBackup>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Role).HasConversion<string>();
        });

        b.Entity<Repository>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
            // Seed the default CAD vault so existing upload/search behaviour is unchanged.
            // A documentation vault is added later with a single additional row.
            e.HasData(new Repository
            {
                Id = WellKnownRepositories.DefaultId,
                Key = WellKnownRepositories.DefaultKey,
                Name = "CAD Files",
                CreatedAt = DateTimeOffset.UnixEpoch   // static value required by HasData
            });
        });

        b.Entity<VaultFile>(e =>
        {
            e.HasKey(x => x.Id);
            // Case-insensitive, extension-agnostic uniqueness (NameKey is already normalized),
            // scoped per vault so "PART-001" can exist independently in CAD and DOCS.
            e.HasIndex(x => new { x.RepositoryId, x.NameKey }).IsUnique();
            e.HasOne(x => x.Repository).WithMany().HasForeignKey(x => x.RepositoryId);
            e.Property(x => x.State).HasConversion<string>();
            e.Property(x => x.ObsoletedFromState).HasConversion<string>();
            e.Property(x => x.Origin).HasConversion<string>();
        });

        b.Entity<FileHistory>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.FileId);
        });

        b.Entity<FileRevisionBackup>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.FileId, x.Revision }).IsUnique();
        });
    }
}
