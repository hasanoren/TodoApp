using Moq;
using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;
using TodoApp.Application.Services;
using TodoApp.Domain.Entities;
using TodoApp.Domain.Exceptions;

namespace TodoApp.Application.Tests;

public class TaskShareServiceTests
{
    private readonly Mock<ITaskShareRepository> _mockTaskShareRepo;
    private readonly Mock<ITodoItemRepository> _mockTodoItemRepo;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly TaskShareService _service;

    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _targetUserId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();

    public TaskShareServiceTests()
    {
        _mockTaskShareRepo = new Mock<ITaskShareRepository>();
        _mockTodoItemRepo = new Mock<ITodoItemRepository>();
        _mockUserRepo = new Mock<IUserRepository>();

        _service = new TaskShareService(
            _mockTaskShareRepo.Object,
            _mockTodoItemRepo.Object,
            _mockUserRepo.Object);
    }

    // --- SHARE TASK TESTS ---

    [Fact]
    public async Task ShareAsync_WhenValid_AddsTaskShare()
    {
        // ARRANGE
        var task = CreateSampleTask(_ownerId, isDeleted: false);
        var targetUser = new User { Id = _targetUserId, Email = "target@example.com" };
        var request = new ShareTaskRequest { Email = "target@example.com" };

        _mockTodoItemRepo.Setup(r => r.GetByIdAsync(task.Id)).ReturnsAsync(task);
        _mockUserRepo.Setup(r => r.GetByEmailAsync("target@example.com")).ReturnsAsync(targetUser);
        _mockTaskShareRepo.Setup(r => r.GetAsync(task.Id, _targetUserId)).ReturnsAsync((TaskShare?)null);

        // ACT
        await _service.ShareAsync(_ownerId, task.Id, request);

        // ASSERT
        _mockTaskShareRepo.Verify(r => r.AddAsync(It.Is<TaskShare>(ts =>
            ts.TaskId == task.Id && ts.UserId == _targetUserId)), Times.Once);
        _mockTaskShareRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // BR-004: Kendi kendine paylaşım engeli
    [Fact]
    public async Task ShareAsync_WhenTargetIsOwner_ThrowsValidationException()
    {
        // ARRANGE
        var task = CreateSampleTask(_ownerId, isDeleted: false);
        var ownerUser = new User { Id = _ownerId, Email = "owner@example.com" };
        var request = new ShareTaskRequest { Email = "owner@example.com" };

        _mockTodoItemRepo.Setup(r => r.GetByIdAsync(task.Id)).ReturnsAsync(task);
        _mockUserRepo.Setup(r => r.GetByEmailAsync("owner@example.com")).ReturnsAsync(ownerUser);

        // ACT & ASSERT
        await Assert.ThrowsAsync<ValidationException>(
            () => _service.ShareAsync(_ownerId, task.Id, request));
    }

    // BR-013 & BR-029: Sadece owner paylaşabilir
    [Fact]
    public async Task ShareAsync_WhenCallerIsNotOwner_ThrowsNotFoundException()
    {
        // ARRANGE
        var task = CreateSampleTask(_ownerId, isDeleted: false);
        var request = new ShareTaskRequest { Email = "target@example.com" };

        _mockTodoItemRepo.Setup(r => r.GetByIdAsync(task.Id)).ReturnsAsync(task);

        // ACT & ASSERT
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.ShareAsync(_otherUserId, task.Id, request));
    }

    // BR-027: Var olmayan kullanıcıyla paylaşım
    [Fact]
    public async Task ShareAsync_WhenTargetUserNotFound_ThrowsNotFoundException()
    {
        // ARRANGE
        var task = CreateSampleTask(_ownerId, isDeleted: false);
        var request = new ShareTaskRequest { Email = "notfound@example.com" };

        _mockTodoItemRepo.Setup(r => r.GetByIdAsync(task.Id)).ReturnsAsync(task);
        _mockUserRepo.Setup(r => r.GetByEmailAsync("notfound@example.com")).ReturnsAsync((User?)null);

        // ACT & ASSERT
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.ShareAsync(_ownerId, task.Id, request));
    }

    // BR-014: Duplicate paylaşım sessizce başarı döner (idempotent)
    [Fact]
    public async Task ShareAsync_WhenAlreadyShared_DoesNotAddDuplicate()
    {
        // ARRANGE
        var task = CreateSampleTask(_ownerId, isDeleted: false);
        var targetUser = new User { Id = _targetUserId, Email = "target@example.com" };
        var existingShare = new TaskShare { TaskId = task.Id, UserId = _targetUserId };
        var request = new ShareTaskRequest { Email = "target@example.com" };

        _mockTodoItemRepo.Setup(r => r.GetByIdAsync(task.Id)).ReturnsAsync(task);
        _mockUserRepo.Setup(r => r.GetByEmailAsync("target@example.com")).ReturnsAsync(targetUser);
        _mockTaskShareRepo.Setup(r => r.GetAsync(task.Id, _targetUserId)).ReturnsAsync(existingShare);

        // ACT
        await _service.ShareAsync(_ownerId, task.Id, request);

        // ASSERT
        _mockTaskShareRepo.Verify(r => r.AddAsync(It.IsAny<TaskShare>()), Times.Never);
    }

    // --- LEAVE SHARE TESTS (BR-028) ---

    [Fact]
    public async Task LeaveShareAsync_WhenUserIsShared_LeavesTask()
    {
        // ARRANGE
        var task = CreateSampleTask(_ownerId, isDeleted: false);
        var share = new TaskShare { TaskId = task.Id, UserId = _targetUserId };

        _mockTodoItemRepo.Setup(r => r.GetByIdAsync(task.Id)).ReturnsAsync(task);
        _mockTaskShareRepo.Setup(r => r.GetAsync(task.Id, _targetUserId)).ReturnsAsync(share);

        // ACT
        await _service.LeaveShareAsync(_targetUserId, task.Id);

        // ASSERT
        _mockTaskShareRepo.Verify(r => r.Remove(share), Times.Once);
        _mockTaskShareRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task LeaveShareAsync_WhenUserIsNotShared_ThrowsNotFoundException()
    {
        // ARRANGE
        var task = CreateSampleTask(_ownerId, isDeleted: false);

        _mockTodoItemRepo.Setup(r => r.GetByIdAsync(task.Id)).ReturnsAsync(task);
        _mockTaskShareRepo.Setup(r => r.GetAsync(task.Id, _targetUserId)).ReturnsAsync((TaskShare?)null);

        // ACT & ASSERT
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.LeaveShareAsync(_targetUserId, task.Id));
    }

    // --- HELPER ---
    private static TodoItem CreateSampleTask(Guid ownerId, bool isDeleted)
    {
        return new TodoItem
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Title = "Test Görevi",
            Status = TodoItemStatus.Open,
            IsDeleted = isDeleted,
            CreatedAt = DateTime.UtcNow
        };
    }
}
