using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TodoApp.Domain.Entities;
using TodoApp.Infrastructure.Data;
using Xunit;

namespace TodoApp.IntegrationTests;

public class DatabaseCascadeIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public DatabaseCascadeIntegrationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using (var command = _connection.CreateCommand())
        {
            command.CommandText = "PRAGMA foreign_keys = ON;";
            command.ExecuteNonQuery();
        }

        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new ApplicationDbContext(_options);

    // BR-002: User silinirse sahip olduğu Task'lar da silinir (ON DELETE CASCADE)
    [Fact]
    public async Task DeleteUser_CascadeDeletes_OwnedTasks()
    {
        // ARRANGE
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "cascade-owner@example.com",
            PasswordHash = "hash",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };

        var task = new TodoItem
        {
            Id = Guid.NewGuid(),
            OwnerId = user.Id,
            Title = "Cascade Test Görevi",
            Status = TodoItemStatus.Open,
            CreatedAt = DateTime.UtcNow
        };

        using (var context = CreateContext())
        {
            context.Users.Add(user);
            context.TodoItems.Add(task);
            await context.SaveChangesAsync();
        }

        // ACT — User silinir
        using (var context = CreateContext())
        {
            var userToDelete = await context.Users.FindAsync(user.Id);
            Assert.NotNull(userToDelete);
            context.Users.Remove(userToDelete);
            await context.SaveChangesAsync();
        }

        // ASSERT — Görev de cascade silinmiş olmalı
        using (var context = CreateContext())
        {
            var remainingTask = await context.TodoItems.FindAsync(task.Id);
            Assert.Null(remainingTask);
        }
    }

    // BR-003 & BR-014: Task silindiğinde veya Task sahibi silindiğinde TaskShare cascade silinir
    [Fact]
    public async Task DeleteTask_CascadeDeletes_TaskShares()
    {
        // ARRANGE
        var owner = new User
        {
            Id = Guid.NewGuid(),
            Email = "task-owner@example.com",
            PasswordHash = "hash",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };

        var sharedUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "shared-user@example.com",
            PasswordHash = "hash",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };

        var task = new TodoItem
        {
            Id = Guid.NewGuid(),
            OwnerId = owner.Id,
            Title = "Paylaşılan Görev",
            Status = TodoItemStatus.Open,
            CreatedAt = DateTime.UtcNow
        };

        var share = new TaskShare
        {
            TaskId = task.Id,
            UserId = sharedUser.Id,
            SharedAt = DateTime.UtcNow
        };

        using (var context = CreateContext())
        {
            context.Users.AddRange(owner, sharedUser);
            context.TodoItems.Add(task);
            context.TaskShares.Add(share);
            await context.SaveChangesAsync();
        }

        // ACT — Görev silinir
        using (var context = CreateContext())
        {
            var taskToDelete = await context.TodoItems.FindAsync(task.Id);
            Assert.NotNull(taskToDelete);
            context.TodoItems.Remove(taskToDelete);
            await context.SaveChangesAsync();
        }

        // ASSERT — TaskShare kaydı cascade silinmiş olmalı
        using (var context = CreateContext())
        {
            var remainingShare = await context.TaskShares.FindAsync(task.Id, sharedUser.Id);
            var remainingUser = await context.Users.FindAsync(sharedUser.Id);

            Assert.Null(remainingShare);
            Assert.NotNull(remainingUser); // Kullanıcı silinmez, sadece paylaşım kaydı silinir
        }
    }

    // BR-019: Üst görev (hard) silinirse tüm alt görevler de silinir (ON DELETE CASCADE)
    [Fact]
    public async Task DeleteTask_CascadeDeletes_SubTasks()
    {
        // ARRANGE
        var owner = new User
        {
            Id = Guid.NewGuid(),
            Email = "subtask-owner@example.com",
            PasswordHash = "hash",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };

        var task = new TodoItem
        {
            Id = Guid.NewGuid(),
            OwnerId = owner.Id,
            Title = "Alt Görevli Ana Görev",
            Status = TodoItemStatus.Open,
            CreatedAt = DateTime.UtcNow
        };

        var subTask1 = new SubTask
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            Title = "Alt Görev 1",
            Status = SubTaskStatus.Open,
            CreatedAt = DateTime.UtcNow
        };

        var subTask2 = new SubTask
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            Title = "Alt Görev 2",
            Status = SubTaskStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };

        using (var context = CreateContext())
        {
            context.Users.Add(owner);
            context.TodoItems.Add(task);
            context.SubTasks.AddRange(subTask1, subTask2);
            await context.SaveChangesAsync();
        }

        // ACT — Ana görev hard delete edilir
        using (var context = CreateContext())
        {
            var taskToDelete = await context.TodoItems.FindAsync(task.Id);
            Assert.NotNull(taskToDelete);
            context.TodoItems.Remove(taskToDelete);
            await context.SaveChangesAsync();
        }

        // ASSERT — Tüm alt görevler de silinmiş olmalı
        using (var context = CreateContext())
        {
            var remainingSubTasks = await context.SubTasks.Where(st => st.TaskId == task.Id).ToListAsync();
            Assert.Empty(remainingSubTasks);
        }
    }

    // User silinince RefreshToken'lar da cascade silinir
    [Fact]
    public async Task DeleteUser_CascadeDeletes_RefreshTokens()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "token-user@example.com",
            PasswordHash = "hash",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };

        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = "sample-refresh-token",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };

        using (var context = CreateContext())
        {
            context.Users.Add(user);
            context.RefreshTokens.Add(token);
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext())
        {
            var userToDelete = await context.Users.FindAsync(user.Id);
            context.Users.Remove(userToDelete!);
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext())
        {
            var remainingToken = await context.RefreshTokens.FindAsync(token.Id);
            Assert.Null(remainingToken);
        }
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}

