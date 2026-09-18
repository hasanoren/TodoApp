using Microsoft.EntityFrameworkCore;
using TodoApp.Domain.Entities;

namespace TodoApp.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<TodoItem> TodoItems => Set<TodoItem>();
    public DbSet<TodoList> TodoLists => Set<TodoList>();
    public DbSet<SubTask> SubTasks => Set<SubTask>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<TodoItemTag> TodoItemTags => Set<TodoItemTag>();
    public DbSet<TaskShare> TaskShares => Set<TaskShare>();
    public DbSet<OwnershipTransferRequest> OwnershipTransferRequests => Set<OwnershipTransferRequest>();
    public DbSet<TodoItemActivity> TodoItemActivities => Set<TodoItemActivity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User yapılandırması (BR-001 & T8.2.6: MaxLength)
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Email).HasMaxLength(256).IsRequired();
            entity.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
        });

        // RefreshToken - User ilişkisi ve indeks (T8.2.6: MaxLength)
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasOne(rt => rt.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(rt => rt.Token).IsUnique();
            entity.Property(rt => rt.Token).HasMaxLength(450).IsRequired();
        });

        // PasswordResetToken - User ilişkisi ve indeks (T8.2.6: MaxLength)
        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.HasOne(prt => prt.User)
                .WithMany(u => u.PasswordResetTokens)
                .HasForeignKey(prt => prt.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(prt => prt.Token).IsUnique();
            entity.Property(prt => prt.Token).HasMaxLength(450).IsRequired();
        });

        // TodoItem - User FK ve Kolon Yapılandırmaları (T8.2.6: MaxLength)
        // OwnerId: CASCADE — User silinirse sahip olduğu görevler de silinir (BR-002)
        modelBuilder.Entity<TodoItem>(entity =>
        {
            entity.HasOne(t => t.Owner)
                .WithMany()
                .HasForeignKey(t => t.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);

            // CompletedByUserId: NO ACTION — SQL Server multiple cascade paths kuralı gereği
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(t => t.CompletedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            // DeletedByUserId: NO ACTION — SQL Server multiple cascade paths kuralı gereği
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(t => t.DeletedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            // T8.2.1: EF Core Global Query Filter (Soft-Delete)
            entity.HasQueryFilter(t => !t.IsDeleted);

            entity.Property(t => t.Title).HasMaxLength(200).IsRequired();
            entity.Property(t => t.Description).HasMaxLength(2000);
        });

        // TodoList yapılandırması (BR-011: Soft-Delete Filter)
        modelBuilder.Entity<TodoList>(entity =>
        {
            entity.HasQueryFilter(l => !l.IsDeleted);
        });

        // SubTask - TodoItem FK ve CASCADE yapılandırması (T8.2.6: MaxLength)
        // BR-016: TaskId NOT NULL
        // BR-019: Üst görev (hard) silinirse tüm alt görevler de silinir (ON DELETE CASCADE)
        modelBuilder.Entity<SubTask>(entity =>
        {
            entity.HasOne(st => st.Task)
                .WithMany(t => t.SubTasks)
                .HasForeignKey(st => st.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(st => st.Title).HasMaxLength(200).IsRequired();
        });

        // Tag yapılandırması (T8.2.6: MaxLength)
        // BR-021: Global etiket adı (Unique)
        // BR-023: Admin silinse bile Tag kalır (CreatedByUserId ON DELETE SET NULL)
        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasIndex(t => t.Name).IsUnique();
            entity.Property(t => t.Name).HasMaxLength(50).IsRequired();

            entity.HasOne(t => t.CreatedByUser)
                .WithMany()
                .HasForeignKey(t => t.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // TodoItemTag Composite Key ve İlişki yapılandırması
        // BR-024: Composite PK (TodoItemId + TagId) — Aynı Tag aynı Task'a iki kez eklenemez
        modelBuilder.Entity<TodoItemTag>()
            .HasKey(tit => new { tit.TodoItemId, tit.TagId });

        modelBuilder.Entity<TodoItemTag>()
            .HasOne(tit => tit.TodoItem)
            .WithMany(t => t.TodoItemTags)
            .HasForeignKey(tit => tit.TodoItemId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TodoItemTag>()
            .HasOne(tit => tit.Tag)
            .WithMany(t => t.TodoItemTags)
            .HasForeignKey(tit => tit.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        // TaskShare Composite Key ve İlişki yapılandırması
        // BR-014: Composite PK (TaskId + UserId)
        modelBuilder.Entity<TaskShare>()
            .HasKey(ts => new { ts.TaskId, ts.UserId });

        modelBuilder.Entity<TaskShare>()
            .HasOne(ts => ts.Task)
            .WithMany(t => t.TaskShares)
            .HasForeignKey(ts => ts.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // SQL Server multiple cascade paths önlemek için NoAction
        modelBuilder.Entity<TaskShare>()
            .HasOne(ts => ts.User)
            .WithMany(u => u.SharedTasks)
            .HasForeignKey(ts => ts.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        // OwnershipTransferRequest yapılandırması
        modelBuilder.Entity<OwnershipTransferRequest>(entity =>
        {
            entity.HasKey(r => r.Id);

            entity.HasOne(r => r.Task)
                .WithMany()
                .HasForeignKey(r => r.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.FromUser)
                .WithMany()
                .HasForeignKey(r => r.FromUserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(r => r.ToUser)
                .WithMany()
                .HasForeignKey(r => r.ToUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}