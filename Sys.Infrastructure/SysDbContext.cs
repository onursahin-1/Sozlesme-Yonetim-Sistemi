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
    public DbSet<ContractTermination> ContractTerminations => Set<ContractTermination>();
    public DbSet<Notification> Notifications => Set<Notification>();
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

        modelBuilder.Entity<Contract>()
            .HasMany(c => c.Terminations)
            .WithOne()
            .HasForeignKey(t => t.ContractId);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        // ContractNo yalnızca sözleşme oluşturulduğunda ("SYBSA..." formatında) atanır;
        // Talep aşamasındaki sözleşmelerde null'dır. Bu yüzden filtreli (yalnızca
        // null olmayanlar için) bir unique index kullanılıyor — aksi halde SQL Server
        // birden fazla null değeri unique index'te reddedebilirdi. Bu index, iki
        // kullanıcının eşzamanlı "Sözleşme Yarat" işleminde aynı numarayı üretmesini
        // veritabanı seviyesinde engeller.
        modelBuilder.Entity<Contract>()
            .HasIndex(c => c.ContractNo)
            .IsUnique()
            .HasFilter("[ContractNo] IS NOT NULL");

        // GetByStageAsync, ReconcileStatusesAsync ve dashboard/liste sorguları sık sık
        // Status/Stage/EndDate üzerinden filtreliyor; şu ana kadar yalnızca foreign-key'ler
        // (örn. CreatedByUserId) ve Username için index vardı. Composite index, Personel
        // rolünün "kendi sözleşmelerim + belirli durumlar" sorgusunu (GetByStatusesAsync)
        // doğrudan karşılıyor.
        modelBuilder.Entity<Contract>()
            .HasIndex(c => c.Status);
        modelBuilder.Entity<Contract>()
            .HasIndex(c => c.Stage);
        modelBuilder.Entity<Contract>()
            .HasIndex(c => c.EndDate);
        modelBuilder.Entity<Contract>()
            .HasIndex(c => new { c.CreatedByUserId, c.Status });

        // Audit log sayfalama (GetAuditLogsPagedAsync) tarih aralığına göre filtreleyip
        // ActionDate'e göre sıralıyor.
        modelBuilder.Entity<AuditLog>()
            .HasIndex(a => a.ActionDate);

        // Zil ikonu her ekran geçişinde okunmamış sayısını sorguluyor; bildirim listesi de
        // kullanıcıya göre filtrelenip tarihe göre sıralanıyor.
        modelBuilder.Entity<Notification>()
            .HasIndex(n => new { n.UserId, n.IsRead });
        modelBuilder.Entity<Notification>()
            .HasIndex(n => n.CreatedAt);

        // Tekrarlayan taramaların aynı olay için mükerrer bildirim üretmesini veritabanı
        // seviyesinde engeller. Olay anında üretilen bildirimlerde DedupeKey null olduğu
        // için index filtreli tanımlandı (birden fazla null serbest).
        modelBuilder.Entity<Notification>()
            .HasIndex(n => new { n.UserId, n.DedupeKey })
            .IsUnique()
            .HasFilter("[DedupeKey] IS NOT NULL");

        modelBuilder.Entity<Notification>()
            .HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ContractItem>()
                    .Property(i => i.UnitPrice).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<Contract>()
            .Property(c => c.TotalAmount).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<ContractRevision>()
            .Property(r => r.PreviousTotalAmount).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<ContractTermination>()
            .Property(t => t.CompensationAmount).HasColumnType("decimal(18,2)");
    }
}