using Moq;
using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;
using TodoApp.Application.Services;
using TodoApp.Domain.Entities;
using TodoApp.Domain.Exceptions;
using Xunit;

namespace TodoApp.Application.Tests;

public class SubTaskServiceTests
{
    private readonly Mock<ISubTaskRepository> _mockSubTaskRepo;
    private readonly Mock<ITodoItemRepository> _mockTodoItemRepo;
    private readonly SubTaskService _service;
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _sharedUserId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();

    public SubTaskServiceTests()
    {
        _mockSubTaskRepo = new Mock<ISubTaskRepository>();
        _mockTodoItemRepo = new Mock<ITodoItemRepository>();
        var authService = new TaskAuthorizationService(_mockTodoItemRepo.Object, _mockSubTaskRepo.Object);
        _service = new SubTaskService(_mockSubTaskRepo.Object, authService);
    }

    // --- CREATE TESTS ---

    [Fact]
    public async Task CreateAsync_WhenValid_AddsSubTaskAndReturnsResponse()
    {
        // ARRANGE
        var parentTask = CreateParentTask(_ownerId, isDeleted: false);

        _mockTodoItemRepo
            .Setup(r => r.GetByIdAsync(parentTask.Id))
            .ReturnsAsync(parentTask);

        var request = new CreateSubTaskRequest { Title = "Veritabanı şemasını çiz" };

        // ACT
        var result = await _service.CreateAsync(_ownerId, parentTask.Id, request);

        // ASSERT
        Assert.Equal("Veritabanı şemasını çiz", result.Title);
        Assert.Equal("Open", result.Status);
        Assert.Equal(parentTask.Id, result.TaskId);
        _mockSubTaskRepo.Verify(r => r.AddAsync(It.IsAny<SubTask>()), Times.Once);
        _mockSubTaskRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // BR-020: Paylaşılan kullanıcı da alt görev ekleyebilmeli
    [Fact]
    public async Task CreateAsync_WhenCalledBySharedUser_AddsSubTaskAndReturnsResponse()
    {
        // ARRANGE
        var parentTask = CreateParentTask(_ownerId, isDeleted: false);
        parentTask.TaskShares.Add(new TaskShare { TaskId = parentTask.Id, UserId = _sharedUserId });

        _mockTodoItemRepo
            .Setup(r => r.GetByIdAsync(parentTask.Id))
            .ReturnsAsync(parentTask);

        var request = new CreateSubTaskRequest { Title = "Paylaşılan kullanıcının eklediği alt görev" };

        // ACT
        var result = await _service.CreateAsync(_sharedUserId, parentTask.Id, request);

        // ASSERT
        Assert.Equal("Paylaşılan kullanıcının eklediği alt görev", result.Title);
        _mockSubTaskRepo.Verify(r => r.AddAsync(It.IsAny<SubTask>()), Times.Once);
        _mockSubTaskRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenTitleIsEmpty_ThrowsValidationException()
    {
        // ARRANGE
        var request = new CreateSubTaskRequest { Title = "   " };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateAsync(_ownerId, Guid.NewGuid(), request));
    }

    // BR-020 & BR-029: Yetkisiz kullanıcı 404 almalı
    [Fact]
    public async Task CreateAsync_WhenUserIsNotParentOwnerOrShared_ThrowsNotFoundException()
    {
        // ARRANGE — üst görev başka kullanıcıya ait
        var parentTask = CreateParentTask(_otherUserId, isDeleted: false);

        _mockTodoItemRepo
            .Setup(r => r.GetByIdAsync(parentTask.Id))
            .ReturnsAsync(parentTask);

        var request = new CreateSubTaskRequest { Title = "Yetkisiz alt görev" };

        // ACT & ASSERT
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.CreateAsync(_ownerId, parentTask.Id, request));
    }

    // BR-012: Silinmiş (soft-delete) bir göreve yeni SubTask eklenemez
    [Fact]
    public async Task CreateAsync_WhenParentTaskIsSoftDeleted_ThrowsValidationException()
    {
        // ARRANGE — üst görev soft-delete edilmiş
        var parentTask = CreateParentTask(_ownerId, isDeleted: true);

        _mockTodoItemRepo
            .Setup(r => r.GetByIdAsync(parentTask.Id))
            .ReturnsAsync(parentTask);

        var request = new CreateSubTaskRequest { Title = "Silinmiş göreve alt görev" };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateAsync(_ownerId, parentTask.Id, request));
    }

    // --- GET LIST TESTS ---

    [Fact]
    public async Task GetByTaskIdAsync_WhenUserIsOwner_ReturnsSubTasks()
    {
        // ARRANGE
        var parentTask = CreateParentTask(_ownerId, isDeleted: false);
        var subTasks = new List<SubTask>
        {
            new SubTask { Id = Guid.NewGuid(), TaskId = parentTask.Id, Title = "Alt görev 1", Status = SubTaskStatus.Open },
            new SubTask { Id = Guid.NewGuid(), TaskId = parentTask.Id, Title = "Alt görev 2", Status = SubTaskStatus.Completed }
        };

        _mockTodoItemRepo
            .Setup(r => r.GetByIdAsync(parentTask.Id))
            .ReturnsAsync(parentTask);

        _mockSubTaskRepo
            .Setup(r => r.GetByTaskIdAsync(parentTask.Id))
            .ReturnsAsync(subTasks);

        // ACT
        var result = await _service.GetByTaskIdAsync(_ownerId, parentTask.Id);

        // ASSERT
        Assert.Equal(2, result.Count);
        Assert.Equal("Alt görev 1", result[0].Title);
        Assert.Equal("Alt görev 2", result[1].Title);
    }

    // BR-018: Üst görev soft-delete ise alt görevler listelenemez
    [Fact]
    public async Task GetByTaskIdAsync_WhenParentTaskIsSoftDeleted_ThrowsNotFoundException()
    {
        // ARRANGE
        var parentTask = CreateParentTask(_ownerId, isDeleted: true);

        _mockTodoItemRepo
            .Setup(r => r.GetByIdAsync(parentTask.Id))
            .ReturnsAsync(parentTask);

        // ACT & ASSERT
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.GetByTaskIdAsync(_ownerId, parentTask.Id));
    }

    // --- COMPLETE TESTS ---

    // BR-017: Üst görev tamamlanınca alt görevlerin durumu değişmez, alt görev tamamlanınca da üst göreve dokunulmaz
    [Fact]
    public async Task CompleteAsync_TogglesStatus_BetweenOpenAndCompleted()
    {
        // ARRANGE
        var parentTask = CreateParentTask(_ownerId, isDeleted: false);
        var subTask = new SubTask
        {
            Id = Guid.NewGuid(),
            TaskId = parentTask.Id,
            Task = parentTask,
            Title = "Test Alt Görev",
            Status = SubTaskStatus.Open
        };

        _mockSubTaskRepo
            .Setup(r => r.GetByIdAsync(subTask.Id))
            .ReturnsAsync(subTask);

        // ACT (1. tamamlama -> Completed olmalı)
        var result1 = await _service.CompleteAsync(_ownerId, subTask.Id);
        Assert.Equal("Completed", result1.Status);
        Assert.Equal(SubTaskStatus.Completed, subTask.Status);

        // ACT (2. tamamlama -> tekrar Open olmalı)
        var result2 = await _service.CompleteAsync(_ownerId, subTask.Id);
        Assert.Equal("Open", result2.Status);
        Assert.Equal(SubTaskStatus.Open, subTask.Status);

        _mockSubTaskRepo.Verify(r => r.SaveChangesAsync(), Times.Exactly(2));
    }

    [Fact]
    public async Task CompleteAsync_WhenCalledBySharedUser_Succeeds()
    {
        // ARRANGE
        var parentTask = CreateParentTask(_ownerId, isDeleted: false);
        parentTask.TaskShares.Add(new TaskShare { TaskId = parentTask.Id, UserId = _sharedUserId });
        var subTask = new SubTask
        {
            Id = Guid.NewGuid(),
            TaskId = parentTask.Id,
            Task = parentTask,
            Title = "Paylaşılan Alt Görev",
            Status = SubTaskStatus.Open
        };

        _mockSubTaskRepo
            .Setup(r => r.GetByIdAsync(subTask.Id))
            .ReturnsAsync(subTask);

        // ACT
        var result = await _service.CompleteAsync(_sharedUserId, subTask.Id);

        // ASSERT
        Assert.Equal("Completed", result.Status);
        _mockSubTaskRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CompleteAsync_WhenUserIsNotOwnerOrShared_ThrowsNotFoundException()
    {
        // ARRANGE
        var parentTask = CreateParentTask(_otherUserId, isDeleted: false);
        var subTask = new SubTask
        {
            Id = Guid.NewGuid(),
            TaskId = parentTask.Id,
            Task = parentTask,
            Title = "Yetkisiz Alt Görev",
            Status = SubTaskStatus.Open
        };

        _mockSubTaskRepo
            .Setup(r => r.GetByIdAsync(subTask.Id))
            .ReturnsAsync(subTask);

        // ACT & ASSERT
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.CompleteAsync(_ownerId, subTask.Id));
    }

    // --- DELETE TESTS ---

    [Fact]
    public async Task DeleteAsync_WhenCalledByOwner_DeletesSubTask()
    {
        // ARRANGE
        var parentTask = CreateParentTask(_ownerId, isDeleted: false);
        var subTask = new SubTask
        {
            Id = Guid.NewGuid(),
            TaskId = parentTask.Id,
            Task = parentTask,
            Title = "Silinecek Alt Görev",
            Status = SubTaskStatus.Open
        };

        _mockSubTaskRepo
            .Setup(r => r.GetByIdAsync(subTask.Id))
            .ReturnsAsync(subTask);

        // ACT
        await _service.DeleteAsync(_ownerId, subTask.Id);

        // ASSERT
        _mockSubTaskRepo.Verify(r => r.Delete(subTask), Times.Once);
        _mockSubTaskRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // BR-020, BR-026: Paylaşılan kullanıcı alt görevi silemez (404 döner)
    [Fact]
    public async Task DeleteAsync_WhenCalledBySharedUser_ThrowsNotFoundException()
    {
        // ARRANGE — Görev owner'a ait ve sharedUserId ile paylaşılmış
        var parentTask = CreateParentTask(_ownerId, isDeleted: false);
        parentTask.TaskShares.Add(new TaskShare { TaskId = parentTask.Id, UserId = _sharedUserId });
        var subTask = new SubTask
        {
            Id = Guid.NewGuid(),
            TaskId = parentTask.Id,
            Task = parentTask,
            Title = "Silinmeyecek Alt Görev",
            Status = SubTaskStatus.Open
        };

        _mockSubTaskRepo
            .Setup(r => r.GetByIdAsync(subTask.Id))
            .ReturnsAsync(subTask);

        // ACT & ASSERT — Paylaşılan kullanıcı alt görevi silemez, 404 almalı
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.DeleteAsync(_sharedUserId, subTask.Id));
    }

    [Fact]
    public async Task DeleteAsync_WhenCalledByStranger_ThrowsNotFoundException()
    {
        // ARRANGE
        var parentTask = CreateParentTask(_ownerId, isDeleted: false);
        var subTask = new SubTask
        {
            Id = Guid.NewGuid(),
            TaskId = parentTask.Id,
            Task = parentTask,
            Title = "Yabancı Alt Görev",
            Status = SubTaskStatus.Open
        };

        _mockSubTaskRepo
            .Setup(r => r.GetByIdAsync(subTask.Id))
            .ReturnsAsync(subTask);

        // ACT & ASSERT
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.DeleteAsync(_otherUserId, subTask.Id));
    }

    // --- HELPER ---
    private static TodoItem CreateParentTask(Guid ownerId, bool isDeleted)
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
