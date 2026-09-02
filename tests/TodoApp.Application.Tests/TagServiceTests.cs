using Moq;
using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;
using TodoApp.Application.Services;
using TodoApp.Domain.Entities;
using TodoApp.Domain.Exceptions;
using Xunit;

namespace TodoApp.Application.Tests;

public class TagServiceTests
{
    private readonly Mock<ITagRepository> _mockTagRepo;
    private readonly Mock<ITodoItemRepository> _mockTodoItemRepo;
    private readonly TagService _service;
    private readonly Guid _adminUserId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();

    public TagServiceTests()
    {
        _mockTagRepo = new Mock<ITagRepository>();
        _mockTodoItemRepo = new Mock<ITodoItemRepository>();
        _service = new TagService(_mockTagRepo.Object, _mockTodoItemRepo.Object);
    }

    // --- CREATE TAG TESTS ---

    [Fact]
    public async Task CreateAsync_WhenValid_AddsTagAndReturnsResponse()
    {
        // ARRANGE
        _mockTagRepo
            .Setup(r => r.GetByNameAsync("Backend"))
            .ReturnsAsync((Tag?)null);

        var request = new CreateTagRequest { Name = "Backend" };

        // ACT
        var result = await _service.CreateAsync(_adminUserId, request);

        // ASSERT
        Assert.Equal("Backend", result.Name);
        _mockTagRepo.Verify(r => r.AddAsync(It.Is<Tag>(t => t.Name == "Backend" && t.CreatedByUserId == _adminUserId)), Times.Once);
        _mockTagRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenNameIsEmpty_ThrowsValidationException()
    {
        // ARRANGE
        var request = new CreateTagRequest { Name = "   " };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateAsync(_adminUserId, request));
    }

    // BR-021: Case-insensitive unique kontrolü
    [Fact]
    public async Task CreateAsync_WhenNameAlreadyExistsCaseInsensitive_ThrowsConflictException()
    {
        // ARRANGE — "backend" küçük harfle zaten var
        var existingTag = new Tag { Id = Guid.NewGuid(), Name = "backend" };

        _mockTagRepo
            .Setup(r => r.GetByNameAsync("BACKEND"))
            .ReturnsAsync(existingTag);

        var request = new CreateTagRequest { Name = "BACKEND" };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ConflictException>(
            () => _service.CreateAsync(_adminUserId, request));
    }

    // --- GET ALL TAGS TESTS ---

    [Fact]
    public async Task GetAllAsync_ReturnsAllTags()
    {
        // ARRANGE
        var tags = new List<Tag>
        {
            new Tag { Id = Guid.NewGuid(), Name = "Backend", CreatedAt = DateTime.UtcNow },
            new Tag { Id = Guid.NewGuid(), Name = "Frontend", CreatedAt = DateTime.UtcNow }
        };

        _mockTagRepo
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(tags);

        // ACT
        var result = await _service.GetAllAsync();

        // ASSERT
        Assert.Equal(2, result.Count);
        Assert.Equal("Backend", result[0].Name);
        Assert.Equal("Frontend", result[1].Name);
    }

    // --- ASSIGN TAG TO TASK TESTS ---

    [Fact]
    public async Task AssignTagToTaskAsync_WhenValid_AddsRelation()
    {
        // ARRANGE
        var task = CreateSampleTask(_ownerId, isDeleted: false);
        var tag = new Tag { Id = Guid.NewGuid(), Name = "Urgent" };

        _mockTodoItemRepo
            .Setup(r => r.GetByIdAsync(task.Id))
            .ReturnsAsync(task);

        _mockTagRepo
            .Setup(r => r.GetByIdAsync(tag.Id))
            .ReturnsAsync(tag);

        _mockTagRepo
            .Setup(r => r.GetTodoItemTagAsync(task.Id, tag.Id))
            .ReturnsAsync((TodoItemTag?)null);

        // ACT
        await _service.AssignTagToTaskAsync(_ownerId, task.Id, tag.Id);

        // ASSERT
        _mockTagRepo.Verify(r => r.AddTodoItemTagAsync(It.Is<TodoItemTag>(tit => tit.TodoItemId == task.Id && tit.TagId == tag.Id)), Times.Once);
        _mockTagRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // BR-024: Aynı etiket aynı göreve iki kez eklenemez
    [Fact]
    public async Task AssignTagToTaskAsync_WhenTagAlreadyAssignedToTask_ThrowsConflictException()
    {
        // ARRANGE
        var task = CreateSampleTask(_ownerId, isDeleted: false);
        var tag = new Tag { Id = Guid.NewGuid(), Name = "Urgent" };
        var existingRelation = new TodoItemTag { TodoItemId = task.Id, TagId = tag.Id };

        _mockTodoItemRepo
            .Setup(r => r.GetByIdAsync(task.Id))
            .ReturnsAsync(task);

        _mockTagRepo
            .Setup(r => r.GetByIdAsync(tag.Id))
            .ReturnsAsync(tag);

        _mockTagRepo
            .Setup(r => r.GetTodoItemTagAsync(task.Id, tag.Id))
            .ReturnsAsync(existingRelation);

        // ACT & ASSERT
        await Assert.ThrowsAsync<ConflictException>(
            () => _service.AssignTagToTaskAsync(_ownerId, task.Id, tag.Id));
    }

    // BR-012: Silinmiş (soft-delete) göreve etiket eklenemez
    [Fact]
    public async Task AssignTagToTaskAsync_WhenTaskIsSoftDeleted_ThrowsValidationException()
    {
        // ARRANGE
        var task = CreateSampleTask(_ownerId, isDeleted: true);
        var tagId = Guid.NewGuid();

        _mockTodoItemRepo
            .Setup(r => r.GetByIdAsync(task.Id))
            .ReturnsAsync(task);

        // ACT & ASSERT
        await Assert.ThrowsAsync<ValidationException>(
            () => _service.AssignTagToTaskAsync(_ownerId, task.Id, tagId));
    }

    // BR-029: Yetkisiz kullanıcı 404 almalı
    [Fact]
    public async Task AssignTagToTaskAsync_WhenUserIsNotTaskOwner_ThrowsNotFoundException()
    {
        // ARRANGE — görev başka bir kullanıcıya ait
        var task = CreateSampleTask(_otherUserId, isDeleted: false);
        var tagId = Guid.NewGuid();

        _mockTodoItemRepo
            .Setup(r => r.GetByIdAsync(task.Id))
            .ReturnsAsync(task);

        // ACT & ASSERT
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.AssignTagToTaskAsync(_ownerId, task.Id, tagId));
    }

    // --- REMOVE TAG FROM TASK TESTS ---

    [Fact]
    public async Task RemoveTagFromTaskAsync_WhenValid_RemovesRelation()
    {
        // ARRANGE
        var task = CreateSampleTask(_ownerId, isDeleted: false);
        var tagId = Guid.NewGuid();
        var relation = new TodoItemTag { TodoItemId = task.Id, TagId = tagId };

        _mockTodoItemRepo
            .Setup(r => r.GetByIdAsync(task.Id))
            .ReturnsAsync(task);

        _mockTagRepo
            .Setup(r => r.GetTodoItemTagAsync(task.Id, tagId))
            .ReturnsAsync(relation);

        // ACT
        await _service.RemoveTagFromTaskAsync(_ownerId, task.Id, tagId);

        // ASSERT
        _mockTagRepo.Verify(r => r.RemoveTodoItemTag(relation), Times.Once);
        _mockTagRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task RemoveTagFromTaskAsync_WhenUserIsNotOwner_ThrowsNotFoundException()
    {
        // ARRANGE
        var task = CreateSampleTask(_otherUserId, isDeleted: false);
        var tagId = Guid.NewGuid();

        _mockTodoItemRepo
            .Setup(r => r.GetByIdAsync(task.Id))
            .ReturnsAsync(task);

        // ACT & ASSERT
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.RemoveTagFromTaskAsync(_ownerId, task.Id, tagId));
    }

    // --- GET TASKS BY TAG ID TESTS ---

    [Fact]
    public async Task GetTasksByTagIdAsync_WhenValid_ReturnsFilteredTasks()
    {
        // ARRANGE
        var tag = new Tag { Id = Guid.NewGuid(), Name = "Backend" };
        var tasks = new List<TodoItem>
        {
            CreateSampleTask(_ownerId, isDeleted: false)
        };

        _mockTagRepo
            .Setup(r => r.GetByIdAsync(tag.Id))
            .ReturnsAsync(tag);

        _mockTagRepo
            .Setup(r => r.GetTodoItemsByTagIdAsync(_ownerId, tag.Id))
            .ReturnsAsync(tasks);

        // ACT
        var result = await _service.GetTasksByTagIdAsync(_ownerId, tag.Id);

        // ASSERT
        Assert.Single(result);
        Assert.Equal("Ana Görev", result[0].Title);
        Assert.Empty(result[0].SubTasks); // Liste görünümünde alt görevler boş döner
    }

    [Fact]
    public async Task GetTasksByTagIdAsync_WhenTagNotFound_ThrowsNotFoundException()
    {
        // ARRANGE
        var nonExistentTagId = Guid.NewGuid();

        _mockTagRepo
            .Setup(r => r.GetByIdAsync(nonExistentTagId))
            .ReturnsAsync((Tag?)null);

        // ACT & ASSERT
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.GetTasksByTagIdAsync(_ownerId, nonExistentTagId));
    }

    // --- HELPER ---
    private static TodoItem CreateSampleTask(Guid ownerId, bool isDeleted)
    {
        return new TodoItem
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Title = "Ana Görev",
            Status = TodoItemStatus.Open,
            IsDeleted = isDeleted,
            CreatedAt = DateTime.UtcNow
        };
    }
}

