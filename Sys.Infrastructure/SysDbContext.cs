using Microsoft.EntityFrameworkCore;
using Sys.Domain;

namespace Sys.Infrastructure;

public class SysDbContext : DbContext
{
    public SysDbContext(DbContextOptions<SysDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ContractItem> ContractItems => Set<ContractItem>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<ApprovalLog> ApprovalLogs => Set<ApprovalLog>();
    public DbSet<Violation> Violations => Set<Violation>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ContractRevision> ContractRevisions => Set<ContractRevision>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Contract>()
            .HasMany(c => c.Items)
            .WithOne()
            .HasForeignKey(i => i.ContractId);

        modelBuilder.Entity<Contract>()
            .HasMany(c => c.Attachments)
            .WithOne()
            .HasForeignKey(a => a.ContractId);

        modelBuilder.Entity<Contract>()
            .HasMany(c => c.ApprovalLogs)
            .WithOne()
            .HasForeignKey(a => a.ContractId);

        modelBuilder.Entity<Contract>()
            .HasMany(c => c.Violations)
            .WithOne()
            .HasForeignKey(v => v.ContractId);

        modelBuilder.Entity<Contract>()
            .HasMany(c => c.Revisions)
            .WithOne()
            .HasForeignKey(r => r.ContractId);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<ContractItem>()
            .Property(i => i.UnitPrice).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<Contract>()
            .Property(c => c.TotalAmount).HasColumnType("decimal(18,2)");
    }
}