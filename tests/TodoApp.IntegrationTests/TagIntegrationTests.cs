using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TodoApp.Application.DTOs;
using TodoApp.Application.Services;
using TodoApp.Domain.Entities;
using TodoApp.Domain.Exceptions;
using TodoApp.Infrastructure.Data;
using TodoApp.Infrastructure.Repositories;
using Xunit;

namespace TodoApp.IntegrationTests;

public class TagIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public TagIntegrationTests()
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

    private async Task<User> CreateAdminUserAsync(ApplicationDbContext context)
    {
        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Email = $"admin_{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(adminUser);
        await context.SaveChangesAsync();
        return adminUser;
    }

    [Fact]
    public async Task CreateAsync_NormalizesToLower_AndGetByNameAsync_FindsTagRegardlessOfInputCase()
    {
        Guid adminId;
        using (var context = CreateContext())
        {
            var admin = await CreateAdminUserAsync(context);
            adminId = admin.Id;
        }

        // 1. Tag oluştur (Karışık harf: "BackendArchitecture")
        using (var context = CreateContext())
        {
            var tagRepo = new TagRepository(context);
            var todoRepo = new TodoItemRepository(context);
            var tagService = new TagService(tagRepo, todoRepo);

            var created = await tagService.CreateAsync(adminId, new CreateTagRequest { Name = "BackendArchitecture" });
            Assert.Equal("backendarchitecture", created.Name);
        }

        // 2. Arama yap: büyük harf, küçük harf ve karışık harf ile sorgula (Index Seek uyumlu)
        using (var context = CreateContext())
        {
            var tagRepo = new TagRepository(context);

            var exactMatch = await tagRepo.GetByNameAsync("BackendArchitecture");
            Assert.NotNull(exactMatch);
            Assert.Equal("backendarchitecture", exactMatch.Name);

            var lowerMatch = await tagRepo.GetByNameAsync("backendarchitecture");
            Assert.NotNull(lowerMatch);
            Assert.Equal("backendarchitecture", lowerMatch.Name);

            var upperMatch = await tagRepo.GetByNameAsync("BACKENDARCHITECTURE");
            Assert.NotNull(upperMatch);
            Assert.Equal("backendarchitecture", upperMatch.Name);
        }
    }

    [Fact]
    public async Task CreateAsync_WhenDuplicateWithDifferentCasing_ThrowsConflictException()
    {
        Guid adminId;
        using (var context = CreateContext())
        {
            var admin = await CreateAdminUserAsync(context);
            adminId = admin.Id;
        }

        using (var context = CreateContext())
        {
            var tagRepo = new TagRepository(context);
            var todoRepo = new TodoItemRepository(context);
            var tagService = new TagService(tagRepo, todoRepo);

            await tagService.CreateAsync(adminId, new CreateTagRequest { Name = "Urgent" });

            // "URGENT" olarak tekrar eklenmeye çalışıldığında ConflictException fırlatılmalı (BR-021)
            await Assert.ThrowsAsync<ConflictException>(
                () => tagService.CreateAsync(adminId, new CreateTagRequest { Name = "URGENT" }));
        }
    }

    [Fact]
    public async Task Tag_UniqueIndex_EnforcesUniqueness()
    {
        using (var context = CreateContext())
        {
            var tagRepo = new TagRepository(context);

            await tagRepo.AddAsync(new Tag
            {
                Id = Guid.NewGuid(),
                Name = "frontend",
                CreatedAt = DateTime.UtcNow
            });
            await tagRepo.SaveChangesAsync();

            // Aynı isimle doğrudan DB seviyesinde ekleme denenirse DbUpdateException fırlatılmalı
            await tagRepo.AddAsync(new Tag
            {
                Id = Guid.NewGuid(),
                Name = "frontend",
                CreatedAt = DateTime.UtcNow
            });

            await Assert.ThrowsAnyAsync<DbUpdateException>(() => tagRepo.SaveChangesAsync());
        }
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}

