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
    public DbSet<SubTask> SubTasks => Set<SubTask>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<TodoItemTag> TodoItemTags => Set<TodoItemTag>();
    public DbSet<TaskShare> TaskShares => Set<TaskShare>();
    public DbSet<OwnershipTransferRequest> OwnershipTransferRequests => Set<OwnershipTransferRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // RefreshToken - User ilişkisi ve indeks
        modelBuilder.Entity<RefreshToken>()
            .HasOne(rt => rt.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RefreshToken>()
            .HasIndex(rt => rt.Token)
            .IsUnique();

        modelBuilder.Entity<PasswordResetToken>()
            .HasOne(prt => prt.User)
            .WithMany(u => u.PasswordResetTokens)
            .HasForeignKey(prt => prt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PasswordResetToken>()
            .HasIndex(prt => prt.Token)
            .IsUnique();

        // TodoItem - User FK yapılandırmaları
        // OwnerId: CASCADE — User silinirse sahip olduğu görevler de silinir (BR-002)
        modelBuilder.Entity<TodoItem>()
            .HasOne(t => t.Owner)
            .WithMany()
            .HasForeignKey(t => t.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        // CompletedByUserId: NO ACTION — SQL Server multiple cascade paths kuralı gereği
        modelBuilder.Entity<TodoItem>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.CompletedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        // DeletedByUserId: NO ACTION — SQL Server multiple cascade paths kuralı gereği
        modelBuilder.Entity<TodoItem>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.DeletedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        // SubTask - TodoItem FK ve CASCADE yapılandırması
        // BR-016: TaskId NOT NULL
        // BR-019: Üst görev (hard) silinirse tüm alt görevler de silinir (ON DELETE CASCADE)
        modelBuilder.Entity<SubTask>()
            .HasOne(st => st.Task)
            .WithMany(t => t.SubTasks)
            .HasForeignKey(st => st.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // Tag yapılandırması
        // BR-021: Global etiket adı (Unique)
        modelBuilder.Entity<Tag>()
            .HasIndex(t => t.Name)
            .IsUnique();

        // BR-023: Admin silinse bile Tag kalır (CreatedByUserId ON DELETE SET NULL)
        modelBuilder.Entity<Tag>()
            .HasOne(t => t.CreatedByUser)
            .WithMany()
            .HasForeignKey(t => t.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

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