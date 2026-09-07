using Moq;
using TodoApp.Application.Interfaces;
using TodoApp.Application.Services;
using TodoApp.Domain.Entities;
using TodoApp.Domain.Exceptions;
using Xunit;

namespace TodoApp.Application.Tests;

public class TaskAuthorizationServiceTests
{
    private readonly Mock<ITodoItemRepository> _mockTodoItemRepo;
    private readonly Mock<ISubTaskRepository> _mockSubTaskRepo;
    private readonly TaskAuthorizationService _authService;

    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _sharedUserId = Guid.NewGuid();
    private readonly Guid _strangerId = Guid.NewGuid();

    public TaskAuthorizationServiceTests()
    {
        _mockTodoItemRepo = new Mock<ITodoItemRepository>();
        _mockSubTaskRepo = new Mock<ISubTaskRepository>();
        _authService = new TaskAuthorizationService(
            _mockTodoItemRepo.Object,
            _mockSubTaskRepo.Object);
    }

    // ==========================================
    // 1. EnsureCanReadAsync Tests
    // ==========================================

    [Fact]
    public async Task EnsureCanReadAsync_WhenUserIsOwner_ReturnsTask()
    {
        var task = CreateSampleTask(_ownerId, isDeleted: false);
        _mockTodoItemRepo.Setup(r => r.GetByIdAsync(task.Id)).ReturnsAsync(task);

        var result = await _authService.EnsureCanReadAsync(task.Id, _ownerId);

        Assert.NotNull(result);
        Assert.Equal(task.Id, result.Id);
    }

    [Fact]
    public async Task EnsureCanReadAsync_WhenUserIsShared_ReturnsTask()
    {
        var task = CreateSampleTask(_ownerId, isDeleted: false);
        task.TaskShares.Add(new TaskShare { TaskId = task.Id, UserId = _sharedUserId });
        _mockTodoItemRepo.Setup(r => r.GetByIdAsync(task.Id)).ReturnsAsync(task);

        var result = await _authService.EnsureCanReadAsync(task.Id, _sharedUserId);

        Assert.NotNull(result);
        Assert.Equal(task.Id, result.Id);
    }

    [Fact]
    public async Task EnsureCanReadAsync_WhenUserIsStranger_ThrowsNotFoundException()
    {
        var task = CreateSampleTask(_ownerId, isDeleted: false);
        _mockTodoItemRepo.Setup(r => r.GetByIdAsync(task.Id)).ReturnsAsync(task);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _authService.EnsureCanReadAsync(task.Id, _strangerId));
    }

    [Fact]
    public async Task EnsureCanReadAsync_WhenTaskIsSoftDeleted_ThrowsNotFoundExceptionForSharedAndStranger()
    {
        var task = CreateSampleTask(_ownerId, isDeleted: true);
        task.TaskShares.Add(new TaskShare { TaskId = task.Id, UserId = _sharedUserId });
        _mockTodoItemRepo.Setup(r => r.GetByIdAsync(task.Id)).ReturnsAsync(task);

        // BR-011: Paylaşılan kullanıcı da yabancı da soft-delete göreve normal erişemez
        await Assert.ThrowsAsync<NotFoundException>(
            () => _authService.EnsureCanReadAsync(task.Id, _sharedUserId));
        await Assert.ThrowsAsync<NotFoundException>(
            () => _authService.EnsureCanReadAsync(task.Id, _strangerId));
    }

    [Fact]
    public async Task EnsureCanReadAsync_WhenTaskIsSoftDeleted_AndAllowTrashTrue_OwnerCanRead_SharedCannot()
    {
        var task = CreateSampleTask(_ownerId, isDeleted: true);
        task.TaskShares.Add(new TaskShare { TaskId = task.Id, UserId = _sharedUserId });
        _mockTodoItemRepo.Setup(r => r.GetByIdAsync(task.Id)).ReturnsAsync(task);

        var ownerResult = await _authService.EnsureCanReadAsync(task.Id, _ownerId, allowTrash: true);
        Assert.NotNull(ownerResult);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _authService.EnsureCanReadAsync(task.Id, _sharedUserId, allowTrash: true));
    }

    // ==========================================
    // 2. EnsureCanModifyAsync & EnsureCanCompleteAsync Tests
    // ==========================================

    [Fact]
    public async Task EnsureCanModifyAsync_WhenOwnerOrShared_Succeeds()
    {
        var task = CreateSampleTask(_ownerId, isDeleted: false);
        task.TaskShares.Add(new TaskShare { TaskId = task.Id, UserId = _sharedUserId });
        _mockTodoItemRepo.Setup(r => r.GetByIdAsync(task.Id)).ReturnsAsync(task);

        var ownerResult = await _authService.EnsureCanModifyAsync(task.Id, _ownerId);
        var sharedResult = await _authService.EnsureCanModifyAsync(task.Id, _sharedUserId);

        Assert.NotNull(ownerResult);
        Assert.NotNull(sharedResult);
    }

    [Fact]
    public async Task EnsureCanCompleteAsync_WhenOwnerOrShared_Succeeds()
    {
        var task = CreateSampleTask(_ownerId, isDeleted: false);
        task.TaskShares.Add(new TaskShare { TaskId = task.Id, UserId = _sharedUserId });
        _mockTodoItemRepo.Setup(r => r.GetByIdAsync(task.Id)).ReturnsAsync(task);

        var ownerResult = await _authService.EnsureCanCompleteAsync(task.Id, _ownerId);
        var sharedResult = await _authService.EnsureCanCompleteAsync(task.Id, _sharedUserId);

        Assert.NotNull(ownerResult);
        Assert.NotNull(sharedResult);
    }

    // ==========================================
    // 3. EnsureCanDeleteAsync Tests (BR-008, BR-026)
    // ==========================================

    [Fact]
    public async Task EnsureCanDeleteAsync_WhenCalledByOwner_ReturnsTask()
    {
        var task = CreateSampleTask(_ownerId, isDeleted: false);
        _mockTodoItemRepo.Setup(r => r.GetByIdAsync(task.Id)).ReturnsAsync(task);

        var result = await _authService.EnsureCanDeleteAsync(task.Id, _ownerId);

        Assert.NotNull(result);
        Assert.Equal(task.Id, result.Id);
    }

    [Fact]
    public async Task EnsureCanDeleteAsync_WhenCalledBySharedUser_ThrowsNotFoundException()
    {
        var task = CreateSampleTask(_ownerId, isDeleted: false);
        task.TaskShares.Add(new TaskShare { TaskId = task.Id, UserId = _sharedUserId });
        _mockTodoItemRepo.Setup(r => r.GetByIdAsync(task.Id)).ReturnsAsync(task);

        // BR-008 & BR-026: Paylaşılan kullanıcı ana görevi silemez
        await Assert.ThrowsAsync<NotFoundException>(
            () => _authService.EnsureCanDeleteAsync(task.Id, _sharedUserId));
    }

    // ==========================================
    // 4. EnsureOwnerAsync Tests
    // ==========================================

    [Fact]
    public async Task EnsureOwnerAsync_WhenCalledBySharedOrStranger_ThrowsNotFoundException()
    {
        var task = CreateSampleTask(_ownerId, isDeleted: false);
        task.TaskShares.Add(new TaskShare { TaskId = task.Id, UserId = _sharedUserId });
        _mockTodoItemRepo.Setup(r => r.GetByIdAsync(task.Id)).ReturnsAsync(task);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _authService.EnsureOwnerAsync(task.Id, _sharedUserId));
        await Assert.ThrowsAsync<NotFoundException>(
            () => _authService.EnsureOwnerAsync(task.Id, _strangerId));
    }

    // ==========================================
    // 5. EnsureCanManageSubTasksAsync Tests (BR-012, BR-020)
    // ==========================================

    [Fact]
    public async Task EnsureCanManageSubTasksAsync_WhenTaskIsSoftDeleted_ThrowsValidationException()
    {
        var task = CreateSampleTask(_ownerId, isDeleted: true);
        _mockTodoItemRepo.Setup(r => r.GetByIdAsync(task.Id)).ReturnsAsync(task);

        // BR-012: Silinmiş göreve alt görev eklenemez
        await Assert.ThrowsAsync<ValidationException>(
            () => _authService.EnsureCanManageSubTasksAsync(task.Id, _ownerId));
    }

    [Fact]
    public async Task EnsureCanManageSubTasksAsync_WhenSharedUser_Succeeds()
    {
        var task = CreateSampleTask(_ownerId, isDeleted: false);
        task.TaskShares.Add(new TaskShare { TaskId = task.Id, UserId = _sharedUserId });
        _mockTodoItemRepo.Setup(r => r.GetByIdAsync(task.Id)).ReturnsAsync(task);

        var result = await _authService.EnsureCanManageSubTasksAsync(task.Id, _sharedUserId);
        Assert.NotNull(result);
    }

    // ==========================================
    // 6. SubTask Complete & Delete Tests (BR-020, BR-026)
    // ==========================================

    [Fact]
    public async Task EnsureCanCompleteSubTaskAsync_WhenCalledBySharedUser_Succeeds()
    {
        var parentTask = CreateSampleTask(_ownerId, isDeleted: false);
        parentTask.TaskShares.Add(new TaskShare { TaskId = parentTask.Id, UserId = _sharedUserId });
        var subTask = new SubTask { Id = Guid.NewGuid(), TaskId = parentTask.Id, Task = parentTask };

        _mockSubTaskRepo.Setup(r => r.GetByIdAsync(subTask.Id)).ReturnsAsync(subTask);

        var result = await _authService.EnsureCanCompleteSubTaskAsync(subTask.Id, _sharedUserId);
        Assert.NotNull(result);
        Assert.Equal(subTask.Id, result.Id);
    }

    [Fact]
    public async Task EnsureCanDeleteSubTaskAsync_WhenCalledByOwner_Succeeds()
    {
        var parentTask = CreateSampleTask(_ownerId, isDeleted: false);
        var subTask = new SubTask { Id = Guid.NewGuid(), TaskId = parentTask.Id, Task = parentTask };

        _mockSubTaskRepo.Setup(r => r.GetByIdAsync(subTask.Id)).ReturnsAsync(subTask);

        var result = await _authService.EnsureCanDeleteSubTaskAsync(subTask.Id, _ownerId);
        Assert.NotNull(result);
        Assert.Equal(subTask.Id, result.Id);
    }

    [Fact]
    public async Task EnsureCanDeleteSubTaskAsync_WhenCalledBySharedUser_ThrowsNotFoundException()
    {
        // KRİTİK KURAL (BR-020, BR-026): Paylaşılan kullanıcı alt görevi SİLEMEZ!
        var parentTask = CreateSampleTask(_ownerId, isDeleted: false);
        parentTask.TaskShares.Add(new TaskShare { TaskId = parentTask.Id, UserId = _sharedUserId });
        var subTask = new SubTask { Id = Guid.NewGuid(), TaskId = parentTask.Id, Task = parentTask };

        _mockSubTaskRepo.Setup(r => r.GetByIdAsync(subTask.Id)).ReturnsAsync(subTask);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _authService.EnsureCanDeleteSubTaskAsync(subTask.Id, _sharedUserId));
    }

    [Fact]
    public async Task EnsureCanDeleteSubTaskAsync_WhenCalledByStranger_ThrowsNotFoundException()
    {
        var parentTask = CreateSampleTask(_ownerId, isDeleted: false);
        var subTask = new SubTask { Id = Guid.NewGuid(), TaskId = parentTask.Id, Task = parentTask };

        _mockSubTaskRepo.Setup(r => r.GetByIdAsync(subTask.Id)).ReturnsAsync(subTask);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _authService.EnsureCanDeleteSubTaskAsync(subTask.Id, _strangerId));
    }

    // --- Helper ---
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

