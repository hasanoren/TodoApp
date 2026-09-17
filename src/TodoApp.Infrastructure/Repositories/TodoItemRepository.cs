using Microsoft.EntityFrameworkCore;
using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;
using TodoApp.Domain.Entities;
using TodoApp.Infrastructure.Data;

namespace TodoApp.Infrastructure.Repositories;

public class TodoItemRepository : ITodoItemRepository
{
    private readonly ApplicationDbContext _context;

    public TodoItemRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TodoItem?> GetByIdAsync(Guid id, bool includeDeleted = false)
    {
        var query = _context.TodoItems.AsQueryable();

        if (includeDeleted)
        {
            query = query.IgnoreQueryFilters();
        }

        return await query
            .Include(t => t.TodoList)
            .Include(t => t.SubTasks)
            .Include(t => t.TodoItemTags)
                .ThenInclude(tit => tit.Tag)
            .Include(t => t.TaskShares)
                .ThenInclude(ts => ts.User)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public Task<(List<TodoItem> Items, int TotalCount)> GetAccessibleByUserAsync(Guid userId, int page, int pageSize)
    {
        return GetAccessibleByUserAsync(userId, new TodoItemFilterDto { Page = page, PageSize = pageSize });
    }

    // BR-011: Soft-delete edilmiş görevler Global Query Filter ile otomatik filtrelenir
    // Liste görünümü için hafif sorgu (SubTasks dahil edilmez, sadece Tag'ler ve Paylaşılanlar dahil edilir)
    // T9.1.1: Dinamik filtreleme (FilterType, Search, Status, DueDate) ve dinamik sıralama
    public async Task<(List<TodoItem> Items, int TotalCount)> GetAccessibleByUserAsync(Guid userId, TodoItemFilterDto filter)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, PaginatedRequest.MaxPageSize);

        var query = _context.TodoItems.AsNoTracking();

        // 1. Paylaşım & Sahiplik Filtresi (FilterType)
        query = filter.FilterType switch
        {
            TaskFilterType.OnlyMine => query.Where(t => t.OwnerId == userId),
            TaskFilterType.SharedWithMe => query.Where(t => t.TaskShares.Any(ts => ts.UserId == userId)),
            TaskFilterType.SharedByMe => query.Where(t => t.OwnerId == userId && t.TaskShares.Any()),
            _ => query.Where(t => t.OwnerId == userId || t.TaskShares.Any(ts => ts.UserId == userId))
        };

        // 2. Serbest Metin Arama (Search)
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(t => t.Title.Contains(search)
                || (t.Description != null && t.Description.Contains(search)));
        }

        // 3. Durum Filtresi (Status)
        if (filter.Status.HasValue)
        {
            query = query.Where(t => t.Status == filter.Status.Value);
        }

        // 4. Teslim Tarihi Aralığı (DueDateFrom, DueDateTo)
        if (filter.DueDateFrom.HasValue)
        {
            query = query.Where(t => t.DueDate >= filter.DueDateFrom.Value);
        }

        if (filter.DueDateTo.HasValue)
        {
            query = query.Where(t => t.DueDate <= filter.DueDateTo.Value);
        }

        if (filter.Priority.HasValue)
        {
            query = query.Where(t => t.Priority == filter.Priority.Value);
        }

        if (filter.TodoListId.HasValue)
        {
            query = query.Where(t => t.TodoListId == filter.TodoListId.Value);
        }

        var totalCount = await query.CountAsync();

        // 5. Dinamik Sıralama (SortBy, SortOrder)
        var isAsc = string.Equals(filter.SortOrder, "asc", StringComparison.OrdinalIgnoreCase);
        var sortBy = filter.SortBy?.Trim().ToLowerInvariant() ?? "createdat";

        query = sortBy switch
        {
            "duedate" => isAsc ? query.OrderBy(t => t.DueDate) : query.OrderByDescending(t => t.DueDate),
            "title" => isAsc ? query.OrderBy(t => t.Title) : query.OrderByDescending(t => t.Title),
            "priority" => isAsc ? query.OrderBy(t => t.Priority) : query.OrderByDescending(t => t.Priority),
            _ => isAsc ? query.OrderBy(t => t.CreatedAt) : query.OrderByDescending(t => t.CreatedAt)
        };

        // 6. Sayfalama (Skip / Take)
        var items = await query
            .Include(t => t.TodoItemTags)
                .ThenInclude(tit => tit.Tag)
            .Include(t => t.TaskShares)
                .ThenInclude(ts => ts.User)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    // Çöp kutusu: sadece owner'ın soft-delete edilmiş görevleri (IgnoreQueryFilters ile filtre muafiyeti)
    public async Task<(List<TodoItem> Items, int TotalCount)> GetDeletedByOwnerAsync(Guid userId, int page, int pageSize)
    {
        var query = _context.TodoItems
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.OwnerId == userId && t.IsDeleted);

        var totalCount = await query.CountAsync();

        var items = await query
            .Include(t => t.SubTasks)
            .Include(t => t.TodoItemTags)
                .ThenInclude(tit => tit.Tag)
            .OrderByDescending(t => t.DeletedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task AddAsync(TodoItem todoItem)
    {
        await _context.TodoItems.AddAsync(todoItem);
    }

    // Hard delete (BR-008a)
    public void Delete(TodoItem todoItem)
    {
        _context.TodoItems.Remove(todoItem);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}

