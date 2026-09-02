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
    }
}