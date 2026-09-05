using Moq;
using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;
using TodoApp.Application.Services;
using TodoApp.Domain.Entities;
using TodoApp.Domain.Exceptions;

namespace TodoApp.Application.Tests;

public class TaskTransferServiceTests
{
    private readonly Mock<IOwnershipTransferRequestRepository> _mockTransferRepo;
    private readonly Mock<ITodoItemRepository> _mockTodoItemRepo;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<ITaskShareRepository> _mockTaskShareRepo;
    private readonly TaskTransferService _service;

    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _targetUserId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();

    public TaskTransferServiceTests()
    {
        _mockTransferRepo = new Mock<IOwnershipTransferRequestRepository>();
        _mockTodoItemRepo = new Mock<ITodoItemRepository>();
        _mockUserRepo = new Mock<IUserRepository>();
        _mockTaskShareRepo = new Mock<ITaskShareRepository>();

        _service = new TaskTransferService(
            _mockTransferRepo.Object,
            _mockTodoItemRepo.Object,
            _mockUserRepo.Object,
            _mockTaskShareRepo.Object);
    }

    // --- CREATE TRANSFER REQUEST ---

    [Fact]
    public async Task CreateTransferRequestAsync_WhenValid_CreatesPendingRequest()
    {
        // ARRANGE
        var task = CreateSampleTask(_ownerId, isDeleted: false);
        var targetUser = new User { Id = _targetUserId, Email = "newowner@example.com" };
        var ownerUser = new User { Id = _ownerId, Email = "owner@example.com" };
        var dto = new CreateTransferRequestDto { NewOwnerEmail = "newowner@example.com" };

        _mockTodoItemRepo.Setup(r => r.GetByIdAsync(task.Id)).ReturnsAsync(task);
        _mockUserRepo.Setup(r => r.GetByEmailAsync("newowner@example.com")).ReturnsAsync(targetUser);
        _mockUserRepo.Setup(r => r.GetByIdAsync(_ownerId)).ReturnsAsync(ownerUser);
        _mockTransferRepo.Setup(r => r.GetActivePendingByTaskIdAsync(task.Id)).ReturnsAsync((OwnershipTransferRequest?)null);

        // ACT
        var result = await _service.CreateTransferRequestAsync(_ownerId, task.Id, dto);

        // ASSERT
        Assert.NotNull(result);
        Assert.Equal(TransferRequestStatus.Pending.ToString(), result.Status);
        Assert.Equal(_targetUserId, result.ToUserId);
        Assert.Equal(_ownerId, result.FromUserId);
        _mockTransferRepo.Verify(r => r.AddAsync(It.Is<OwnershipTransferRequest>(tr =>
            tr.TaskId == task.Id && tr.ToUserId == _targetUserId && tr.Status == TransferRequestStatus.Pending)), Times.Once);
        _mockTransferRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateTransferRequestAsync_WhenCallerIsNotOwner_ThrowsNotFoundException()
    {
        // ARRANGE
        var task = CreateSampleTask(_ownerId, isDeleted: false);
        var dto = new CreateTransferRequestDto { NewOwnerEmail = "newowner@example.com" };

        _mockTodoItemRepo.Setup(r => r.GetByIdAsync(task.Id)).ReturnsAsync(task);

        // ACT & ASSERT
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.CreateTransferRequestAsync(_otherUserId, task.Id, dto));
    }

    [Fact]
    public async Task CreateTransferRequestAsync_WhenTargetIsSelf_ThrowsValidationException()
    {
        // ARRANGE
        var task = CreateSampleTask(_ownerId, isDeleted: false);
        var selfUser = new User { Id = _ownerId, Email = "owner@example.com" };
        var dto = new CreateTransferRequestDto { NewOwnerEmail = "owner@example.com" };

        _mockTodoItemRepo.Setup(r => r.GetByIdAsync(task.Id)).ReturnsAsync(task);
        _mockUserRepo.Setup(r => r.GetByEmailAsync("owner@example.com")).ReturnsAsync(selfUser);

        // ACT & ASSERT
        await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateTransferRequestAsync(_ownerId, task.Id, dto));
    }

    [Fact]
    public async Task CreateTransferRequestAsync_WhenActivePendingExists_ThrowsConflictException()
    {
        // ARRANGE
        var task = CreateSampleTask(_ownerId, isDeleted: false);
        var targetUser = new User { Id = _targetUserId, Email = "newowner@example.com" };
        var dto = new CreateTransferRequestDto { NewOwnerEmail = "newowner@example.com" };
        var existingRequest = new OwnershipTransferRequest { Id = Guid.NewGuid(), TaskId = task.Id, Status = TransferRequestStatus.Pending };

        _mockTodoItemRepo.Setup(r => r.GetByIdAsync(task.Id)).ReturnsAsync(task);
        _mockUserRepo.Setup(r => r.GetByEmailAsync("newowner@example.com")).ReturnsAsync(targetUser);
        _mockTransferRepo.Setup(r => r.GetActivePendingByTaskIdAsync(task.Id)).ReturnsAsync(existingRequest);

        // ACT & ASSERT
        await Assert.ThrowsAsync<ConflictException>(
            () => _service.CreateTransferRequestAsync(_ownerId, task.Id, dto));
    }

    // --- GET PENDING REQUESTS ---

    [Fact]
    public async Task GetPendingRequestsAsync_ReturnsOnlyPendingForUser()
    {
        // ARRANGE
        var requests = new List<OwnershipTransferRequest>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TaskId = Guid.NewGuid(),
                Task = new TodoItem { Title = "Görev 1" },
                FromUserId = _ownerId,
                FromUser = new User { Email = "owner@example.com" },
                ToUserId = _targetUserId,
                ToUser = new User { Email = "target@example.com" },
                Status = TransferRequestStatus.Pending,
                CreatedAt = DateTime.UtcNow
            }
        };

        _mockTransferRepo.Setup(r => r.GetPendingByToUserIdAsync(_targetUserId)).ReturnsAsync(requests);

        // ACT
        var result = await _service.GetPendingRequestsAsync(_targetUserId);

        // ASSERT
        Assert.Single(result);
        Assert.Equal("Görev 1", result[0].TaskTitle);
        Assert.Equal("owner@example.com", result[0].FromUserEmail);
    }

    // --- ACCEPT TRANSFER REQUEST ---

    [Fact]
    public async Task AcceptTransferRequestAsync_WhenValid_TransfersOwnershipAndUpdatesStatus()
    {
        // ARRANGE
        var task = CreateSampleTask(_ownerId, isDeleted: false);
        var request = new OwnershipTransferRequest
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            FromUserId = _ownerId,
            ToUserId = _targetUserId,
            Status = TransferRequestStatus.Pending
        };
        var existingShare = new TaskShare { TaskId = task.Id, UserId = _targetUserId };

        _mockTransferRepo.Setup(r => r.GetByIdAsync(request.Id)).ReturnsAsync(request);
        _mockTodoItemRepo.Setup(r => r.GetByIdAsync(task.Id)).ReturnsAsync(task);
        _mockTaskShareRepo.Setup(r => r.GetAsync(task.Id, _targetUserId)).ReturnsAsync(existingShare);

        // ACT
        await _service.AcceptTransferRequestAsync(_targetUserId, request.Id);

        // ASSERT
        Assert.Equal(_targetUserId, task.OwnerId); // Sahiplik aktarıldı
        Assert.Equal(TransferRequestStatus.Accepted, request.Status); // Talep kabul edildi
        Assert.NotNull(request.RespondedAt);
        _mockTaskShareRepo.Verify(r => r.Remove(existingShare), Times.Once); // Yeni sahip paylaşılandan silindi
        _mockTaskShareRepo.Verify(r => r.AddAsync(It.Is<TaskShare>(ts =>
            ts.TaskId == task.Id && ts.UserId == _ownerId)), Times.Once); // Eski sahip paylaşılanlara eklendi
        _mockTodoItemRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
        _mockTransferRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task AcceptTransferRequestAsync_WhenCallerIsNotTarget_ThrowsNotFoundException()
    {
        // ARRANGE
        var request = new OwnershipTransferRequest
        {
            Id = Guid.NewGuid(),
            FromUserId = _ownerId,
            ToUserId = _targetUserId,
            Status = TransferRequestStatus.Pending
        };

        _mockTransferRepo.Setup(r => r.GetByIdAsync(request.Id)).ReturnsAsync(request);

        // ACT & ASSERT
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.AcceptTransferRequestAsync(_otherUserId, request.Id));
    }

    // --- REJECT TRANSFER REQUEST ---

    [Fact]
    public async Task RejectTransferRequestAsync_WhenValid_SetsStatusRejected()
    {
        // ARRANGE
        var request = new OwnershipTransferRequest
        {
            Id = Guid.NewGuid(),
            FromUserId = _ownerId,
            ToUserId = _targetUserId,
            Status = TransferRequestStatus.Pending
        };

        _mockTransferRepo.Setup(r => r.GetByIdAsync(request.Id)).ReturnsAsync(request);

        // ACT
        await _service.RejectTransferRequestAsync(_targetUserId, request.Id);

        // ASSERT
        Assert.Equal(TransferRequestStatus.Rejected, request.Status);
        Assert.NotNull(request.RespondedAt);
        _mockTransferRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // --- CANCEL TRANSFER REQUEST ---

    [Fact]
    public async Task CancelTransferRequestAsync_WhenCalledByOwner_SetsStatusCancelled()
    {
        // ARRANGE
        var request = new OwnershipTransferRequest
        {
            Id = Guid.NewGuid(),
            FromUserId = _ownerId,
            ToUserId = _targetUserId,
            Status = TransferRequestStatus.Pending
        };

        _mockTransferRepo.Setup(r => r.GetByIdAsync(request.Id)).ReturnsAsync(request);

        // ACT
        await _service.CancelTransferRequestAsync(_ownerId, request.Id);

        // ASSERT
        Assert.Equal(TransferRequestStatus.Cancelled, request.Status);
        Assert.NotNull(request.RespondedAt);
        _mockTransferRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CancelTransferRequestAsync_WhenCallerIsNotOwner_ThrowsNotFoundException()
    {
        // ARRANGE
        var request = new OwnershipTransferRequest
        {
            Id = Guid.NewGuid(),
            FromUserId = _ownerId,
            ToUserId = _targetUserId,
            Status = TransferRequestStatus.Pending
        };

        _mockTransferRepo.Setup(r => r.GetByIdAsync(request.Id)).ReturnsAsync(request);

        // ACT & ASSERT
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.CancelTransferRequestAsync(_otherUserId, request.Id));
    }

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

