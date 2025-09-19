using EasyPatchy3.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace EasyPatchy3.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<AppVersion> Versions { get; set; } = null!;
        public DbSet<Patch> Patches { get; set; } = null!;
        public DbSet<Download> Downloads { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AppVersion>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Hash).IsRequired().HasMaxLength(64);
                entity.Property(e => e.StoragePath).IsRequired().HasMaxLength(500);
                entity.HasIndex(e => e.Name).IsUnique();
                entity.HasIndex(e => e.Hash);
            });

            modelBuilder.Entity<Patch>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.PatchFilePath).IsRequired().HasMaxLength(500);

                entity.HasOne(p => p.SourceVersion)
                    .WithMany(v => v.SourcePatches)
                    .HasForeignKey(p => p.SourceVersionId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.TargetVersion)
                    .WithMany(v => v.TargetPatches)
                    .HasForeignKey(p => p.TargetVersionId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => new { e.SourceVersionId, e.TargetVersionId }).IsUnique();
            });

            modelBuilder.Entity<Download>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FileName).IsRequired().HasMaxLength(500);
                entity.Property(e => e.ClientIp).HasMaxLength(45);

                entity.HasOne(d => d.Version)
                    .WithMany()
                    .HasForeignKey(d => d.VersionId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.Patch)
                    .WithMany()
                    .HasForeignKey(d => d.PatchId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.DownloadedAt);
            });
        }
    }
}