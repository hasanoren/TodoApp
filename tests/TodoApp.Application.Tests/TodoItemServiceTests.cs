using Moq;
using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;
using TodoApp.Application.Services;
using TodoApp.Domain.Entities;
using TodoApp.Domain.Exceptions;
using Xunit;

namespace TodoApp.Application.Tests;

public class TodoItemServiceTests
{
    private readonly Mock<ITodoItemRepository> _mockRepo;
    private readonly TodoItemService _service;
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();

    public TodoItemServiceTests()
    {
        _mockRepo = new Mock<ITodoItemRepository>();
        _service = new TodoItemService(_mockRepo.Object);
    }

    // --- BR-029: Yetkisiz erişimde 404 ---

    [Fact]
    public async Task GetByIdAsync_WhenUserIsNotOwner_ThrowsNotFoundException()
    {
        // ARRANGE — görev başka bir kullanıcıya ait
        var todoItem = CreateSampleTodoItem(_otherUserId);

        _mockRepo
            .Setup(r => r.GetByIdAsync(todoItem.Id))
            .ReturnsAsync(todoItem);

        // ACT & ASSERT — yetkisiz kullanıcı 404 almalı (403 değil, BR-029)
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.GetByIdAsync(_ownerId, todoItem.Id));
    }

    [Fact]
    public async Task GetByIdAsync_WhenItemDoesNotExist_ThrowsNotFoundException()
    {
        // ARRANGE
        var nonExistentId = Guid.NewGuid();

        _mockRepo
            .Setup(r => r.GetByIdAsync(nonExistentId))
            .ReturnsAsync((TodoItem?)null);

        // ACT & ASSERT
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.GetByIdAsync(_ownerId, nonExistentId));
    }

    // --- BR-008a: Owner silerse hard delete ---

    [Fact]
    public async Task DeleteAsync_WhenCalledByOwner_HardDeletesItem()
    {
        // ARRANGE
        var todoItem = CreateSampleTodoItem(_ownerId);

        _mockRepo
            .Setup(r => r.GetByIdAsync(todoItem.Id))
            .ReturnsAsync(todoItem);

        // ACT
        await _service.DeleteAsync(_ownerId, todoItem.Id);

        // ASSERT — repo.Delete çağrıldığını doğrula (hard delete)
        _mockRepo.Verify(r => r.Delete(todoItem), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // --- BR-009: Tamamlanmış görev de silinebilir ---

    [Fact]
    public async Task DeleteAsync_WhenItemIsCompleted_StillDeletes()
    {
        // ARRANGE — tamamlanmış bir görev
        var todoItem = CreateSampleTodoItem(_ownerId);
        todoItem.Status = TodoItemStatus.Completed;
        todoItem.CompletedByUserId = _ownerId;
        todoItem.CompletedAt = DateTime.UtcNow;

        _mockRepo
            .Setup(r => r.GetByIdAsync(todoItem.Id))
            .ReturnsAsync(todoItem);

        // ACT — tamamlanmış olsa bile silinebilmeli
        await _service.DeleteAsync(_ownerId, todoItem.Id);

        // ASSERT
        _mockRepo.Verify(r => r.Delete(todoItem), Times.Once);
    }

    // --- BR-015: CompletedByUserId ve CompletedAt set edilir ---

    [Fact]
    public async Task CompleteAsync_SetsCompletedByAndCompletedAt()
    {
        // ARRANGE
        var todoItem = CreateSampleTodoItem(_ownerId);

        _mockRepo
            .Setup(r => r.GetByIdAsync(todoItem.Id))
            .ReturnsAsync(todoItem);

        // ACT
        var result = await _service.CompleteAsync(_ownerId, todoItem.Id);

        // ASSERT
        Assert.Equal("Completed", result.Status);
        Assert.Equal(_ownerId, result.CompletedByUserId);
        Assert.NotNull(result.CompletedAt);
        Assert.Equal(TodoItemStatus.Completed, todoItem.Status);
        _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // --- BR-010: Sadece owner restore edebilir ---

    [Fact]
    public async Task RestoreAsync_WhenCalledByOwner_RestoresItem()
    {
        // ARRANGE — soft-delete edilmiş bir görev
        var todoItem = CreateSampleTodoItem(_ownerId);
        todoItem.IsDeleted = true;
        todoItem.DeletedByUserId = _otherUserId;
        todoItem.DeletedAt = DateTime.UtcNow;

        _mockRepo
            .Setup(r => r.GetByIdAsync(todoItem.Id))
            .ReturnsAsync(todoItem);

        // ACT
        var result = await _service.RestoreAsync(_ownerId, todoItem.Id);

        // ASSERT
        Assert.False(todoItem.IsDeleted);
        Assert.Null(todoItem.DeletedByUserId);
        Assert.Null(todoItem.DeletedAt);
    }

    [Fact]
    public async Task RestoreAsync_WhenCalledByNonOwner_ThrowsNotFoundException()
    {
        // ARRANGE — görev başka birinin
        var todoItem = CreateSampleTodoItem(_otherUserId);
        todoItem.IsDeleted = true;

        _mockRepo
            .Setup(r => r.GetByIdAsync(todoItem.Id))
            .ReturnsAsync(todoItem);

        // ACT & ASSERT — owner olmayan kullanıcı 404 almalı
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.RestoreAsync(_ownerId, todoItem.Id));
    }

    // --- BR-011: Soft-delete edilmiş görev aktif listede görünmemeli ---

    [Fact]
    public async Task GetByIdAsync_WhenItemIsSoftDeleted_ThrowsNotFoundException()
    {
        // ARRANGE
        var todoItem = CreateSampleTodoItem(_ownerId);
        todoItem.IsDeleted = true;

        _mockRepo
            .Setup(r => r.GetByIdAsync(todoItem.Id))
            .ReturnsAsync(todoItem);

        // ACT & ASSERT — soft-delete edilmiş görev erişilemez olmalı
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.GetByIdAsync(_ownerId, todoItem.Id));
    }

    [Fact]
    public async Task GetByIdAsync_WhenValidWithSubTasksAndTags_MapsNestedCollectionsCorrectly()
    {
        // ARRANGE
        var todoItem = CreateSampleTodoItem(_ownerId);
        var tag = new Tag { Id = Guid.NewGuid(), Name = "Urgent", CreatedAt = DateTime.UtcNow };

        todoItem.SubTasks.Add(new SubTask
        {
            Id = Guid.NewGuid(),
            TaskId = todoItem.Id,
            Title = "Alt Görev 1",
            Status = SubTaskStatus.Open,
            CreatedAt = DateTime.UtcNow
        });

        todoItem.TodoItemTags.Add(new TodoItemTag
        {
            TodoItemId = todoItem.Id,
            TagId = tag.Id,
            Tag = tag,
            AssignedAt = DateTime.UtcNow
        });

        _mockRepo
            .Setup(r => r.GetByIdAsync(todoItem.Id))
            .ReturnsAsync(todoItem);

        // ACT
        var result = await _service.GetByIdAsync(_ownerId, todoItem.Id);

        // ASSERT
        Assert.NotNull(result);
        Assert.Single(result.SubTasks);
        Assert.Equal("Alt Görev 1", result.SubTasks[0].Title);
        Assert.Single(result.Tags);
        Assert.Equal("Urgent", result.Tags[0].Name);
    }

    // --- Yardımcı ---

    private TodoItem CreateSampleTodoItem(Guid ownerId)
    {
        return new TodoItem
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Title = "Test Görevi",
            Description = "Test açıklaması",
            Status = TodoItemStatus.Open,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
    }
}

