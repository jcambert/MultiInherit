using Microsoft.EntityFrameworkCore;
using MultiInherit.Todo.Data;
using MultiInherit.Todo.Models;

namespace MultiInherit.Todo.Services;

public class TodoService(IDbContextFactory<TodoDbContext> factory) : ITodoService
{
    // Npgsql exige Kind=Utc pour timestamptz.
    // .Date retourne Kind=Unspecified → on reconstruit avec Kind=Utc.
    private static DateTime UtcToday =>
        new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, 0, 0, 0, DateTimeKind.Utc);

    // Force Kind=Utc sur une valeur venant de l'UI (MudDatePicker → Kind=Unspecified).
    private static DateTime? UtcOnly(DateTime? dt) =>
        dt.HasValue ? DateTime.SpecifyKind(dt.Value, DateTimeKind.Utc) : null;

    // ── Tasks ─────────────────────────────────────────────────────────────

    public async Task<List<TodoTask>> GetTasksAsync(TaskFilter? filter = null)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var q = ctx.Set<TodoTask>()
            .Include(t => t.Project)
            .Include(t => t.Tags)
            .Include(t => t.ParentTask)
            .AsQueryable();

        if (filter != null)
        {
            if (filter.ProjectId.HasValue)
                q = q.Where(t => t.ProjectId == filter.ProjectId);

            if (!string.IsNullOrEmpty(filter.Status))
                q = q.Where(t => t.Status == filter.Status);

            if (!string.IsNullOrEmpty(filter.Priority))
                q = q.Where(t => t.Priority == filter.Priority);

            if (filter.TagId.HasValue)
                q = q.Where(t => t.Tags.Any(tag => tag.Id == filter.TagId));

            if (!string.IsNullOrEmpty(filter.Search))
            {
                var s = filter.Search.ToLower();
                q = q.Where(t => t.Title.ToLower().Contains(s)
                              || (t.Description != null && t.Description.ToLower().Contains(s)));
            }

            if (!filter.ShowCompleted)
                q = q.Where(t => t.Status != "done");

            if (!filter.ShowCancelled)
                q = q.Where(t => t.Status != "cancelled");

            if (filter.OverdueOnly)
                q = q.Where(t => t.DueDate.HasValue
                              && t.DueDate.Value < UtcToday
                              && t.Status != "done"
                              && t.Status != "cancelled");

            if (filter.NoProjectOnly)
                q = q.Where(t => t.ProjectId == null);
        }

        // Le tri par priorité (PriorityOrder) ne peut pas être traduit en SQL :
        // on matérialise d'abord, puis on trie entièrement en mémoire.
        var results = await q.ToListAsync();
        return [.. results
            .OrderBy(t => t.DueDate == null ? 1 : 0)
            .ThenBy(t => t.DueDate)
            .ThenByDescending(t => PriorityOrder(t.Priority))];
    }

    public async Task<TodoTask?> GetTaskByIdAsync(int id)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        return await ctx.Set<TodoTask>()
            .Include(t => t.Project)
            .Include(t => t.Tags)
            .Include(t => t.ParentTask)
            .Include(t => t.Subtasks).ThenInclude(s => s.Tags)
            .Include(t => t.Comments.OrderByDescending(c => c.CreatedAt))
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<TodoTask> CreateTaskAsync(TaskUpsertDto dto)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var task = new TodoTask
        {
            Title            = dto.Title,
            Description      = dto.Description,
            Priority         = dto.Priority,
            Status           = dto.Status,
            DueDate          = UtcOnly(dto.DueDate),
            ProjectId        = dto.ProjectId,
            ParentTaskId     = dto.ParentTaskId,
            EstimatedMinutes = dto.EstimatedMinutes
        };
        ctx.Set<TodoTask>().Add(task);
        await ctx.SaveChangesAsync();

        if (dto.TagIds.Count > 0)
        {
            var loaded = await ctx.Set<TodoTask>().Include(t => t.Tags).FirstAsync(t => t.Id == task.Id);
            var tags = await ctx.Set<TodoTag>().Where(t => dto.TagIds.Contains(t.Id)).ToListAsync();
            foreach (var tag in tags) loaded.Tags.Add(tag);
            await ctx.SaveChangesAsync();
        }

        return (await GetTaskByIdAsync(task.Id))!;
    }

    public async Task<TodoTask> UpdateTaskAsync(int id, TaskUpsertDto dto)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var task = await ctx.Set<TodoTask>().Include(t => t.Tags).FirstAsync(t => t.Id == id);

        task.Title            = dto.Title;
        task.Description      = dto.Description;
        task.Priority         = dto.Priority;
        task.Status           = dto.Status;
        task.DueDate          = UtcOnly(dto.DueDate);
        task.ProjectId        = dto.ProjectId;
        task.ParentTaskId     = dto.ParentTaskId;
        task.EstimatedMinutes = dto.EstimatedMinutes;

        if (dto.Status == "done" && task.CompletedAt == null)
            task.CompletedAt = DateTime.UtcNow;
        else if (dto.Status != "done")
            task.CompletedAt = null;

        // Synchroniser les tags
        task.Tags.Clear();
        if (dto.TagIds.Count > 0)
        {
            var tags = await ctx.Set<TodoTag>().Where(t => dto.TagIds.Contains(t.Id)).ToListAsync();
            foreach (var tag in tags) task.Tags.Add(tag);
        }

        await ctx.SaveChangesAsync();
        return (await GetTaskByIdAsync(id))!;
    }

    public async Task CompleteTaskAsync(int id)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var task = await ctx.Set<TodoTask>().FindAsync(id) ?? throw new KeyNotFoundException();
        task.Status      = "done";
        task.CompletedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    public async Task ReopenTaskAsync(int id)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var task = await ctx.Set<TodoTask>().FindAsync(id) ?? throw new KeyNotFoundException();
        task.Status      = "todo";
        task.CompletedAt = null;
        await ctx.SaveChangesAsync();
    }

    public async Task DeleteTaskAsync(int id)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var task = await ctx.Set<TodoTask>().FindAsync(id);
        if (task != null)
        {
            ctx.Set<TodoTask>().Remove(task);
            await ctx.SaveChangesAsync();
        }
    }

    public async Task<int> GetOverdueCountAsync()
    {
        await using var ctx = await factory.CreateDbContextAsync();
        return await ctx.Set<TodoTask>().CountAsync(t =>
            t.DueDate.HasValue
            && t.DueDate.Value < UtcToday
            && t.Status != "done"
            && t.Status != "cancelled");
    }

    // ── Projects ──────────────────────────────────────────────────────────

    public async Task<List<TodoProject>> GetProjectsAsync(bool includeArchived = false)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var q = ctx.Set<TodoProject>().AsQueryable();
        if (!includeArchived) q = q.Where(p => !p.IsArchived);
        return await q.OrderBy(p => p.Name).ToListAsync();
    }

    public async Task<TodoProject> CreateProjectAsync(string name, string? description, string color)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var project = new TodoProject { Name = name, Description = description, Color = color };
        ctx.Set<TodoProject>().Add(project);
        await ctx.SaveChangesAsync();
        return project;
    }

    public async Task<TodoProject> UpdateProjectAsync(int id, string name, string? description, string color)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var project = await ctx.Set<TodoProject>().FindAsync(id) ?? throw new KeyNotFoundException();
        project.Name        = name;
        project.Description = description;
        project.Color       = color;
        await ctx.SaveChangesAsync();
        return project;
    }

    public async Task ArchiveProjectAsync(int id, bool archive)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var project = await ctx.Set<TodoProject>().FindAsync(id) ?? throw new KeyNotFoundException();
        project.IsArchived = archive;
        await ctx.SaveChangesAsync();
    }

    public async Task DeleteProjectAsync(int id)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var project = await ctx.Set<TodoProject>().FindAsync(id);
        if (project != null) { ctx.Set<TodoProject>().Remove(project); await ctx.SaveChangesAsync(); }
    }

    public async Task<Dictionary<int, int>> GetTaskCountByProjectAsync()
    {
        await using var ctx = await factory.CreateDbContextAsync();
        return await ctx.Set<TodoTask>()
            .Where(t => t.ProjectId.HasValue && t.Status != "done" && t.Status != "cancelled")
            .GroupBy(t => t.ProjectId!.Value)
            .Select(g => new { ProjectId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProjectId, x => x.Count);
    }

    // ── Tags ──────────────────────────────────────────────────────────────

    public async Task<List<TodoTag>> GetTagsAsync()
    {
        await using var ctx = await factory.CreateDbContextAsync();
        return await ctx.Set<TodoTag>().OrderBy(t => t.Label).ToListAsync();
    }

    public async Task<TodoTag> CreateTagAsync(string label, string color)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var tag = new TodoTag { Label = label, Color = color };
        ctx.Set<TodoTag>().Add(tag);
        await ctx.SaveChangesAsync();
        return tag;
    }

    public async Task<TodoTag> UpdateTagAsync(int id, string label, string color)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var tag = await ctx.Set<TodoTag>().FindAsync(id) ?? throw new KeyNotFoundException();
        tag.Label = label;
        tag.Color = color;
        await ctx.SaveChangesAsync();
        return tag;
    }

    public async Task DeleteTagAsync(int id)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var tag = await ctx.Set<TodoTag>().FindAsync(id);
        if (tag != null) { ctx.Set<TodoTag>().Remove(tag); await ctx.SaveChangesAsync(); }
    }

    // ── Comments ──────────────────────────────────────────────────────────

    public async Task<List<TodoComment>> GetCommentsAsync(int taskId)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        return await ctx.Set<TodoComment>()
            .Where(c => c.TaskId == taskId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<TodoComment> AddCommentAsync(int taskId, string content, string author = "Me")
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var comment = new TodoComment
        {
            TaskId    = taskId,
            Content   = content,
            Author    = author,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Set<TodoComment>().Add(comment);
        await ctx.SaveChangesAsync();
        return comment;
    }

    public async Task DeleteCommentAsync(int id)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var comment = await ctx.Set<TodoComment>().FindAsync(id);
        if (comment != null) { ctx.Set<TodoComment>().Remove(comment); await ctx.SaveChangesAsync(); }
    }

    // ── Stats ─────────────────────────────────────────────────────────────

    public async Task<TaskStats> GetStatsAsync()
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var tasks = await ctx.Set<TodoTask>().Include(t => t.Project).ToListAsync();

        var overdue = tasks.Count(t =>
            t.DueDate.HasValue
            && t.DueDate.Value.Date < UtcToday
            && t.Status != "done"
            && t.Status != "cancelled");

        var byPriority = tasks
            .GroupBy(t => t.Priority)
            .ToDictionary(g => g.Key, g => g.Count());

        var byProject = tasks
            .Where(t => t.Project != null)
            .GroupBy(t => t.Project!.Name)
            .ToDictionary(g => g.Key, g => g.Count());

        return new TaskStats(
            Total:      tasks.Count,
            Overdue:    overdue,
            DueToday:   tasks.Count(t => t.DueDate.HasValue && t.DueDate.Value.Date == UtcToday),
            Completed:  tasks.Count(t => t.Status == "done"),
            InProgress: tasks.Count(t => t.Status == "in_progress"),
            Todo:       tasks.Count(t => t.Status == "todo"),
            Cancelled:  tasks.Count(t => t.Status == "cancelled"),
            ByPriority: byPriority,
            ByProject:  byProject);
    }

    // ── Helper ────────────────────────────────────────────────────────────

    private static int PriorityOrder(string priority) => priority switch
    {
        "urgent" => 4,
        "high"   => 3,
        "normal" => 2,
        "low"    => 1,
        _        => 0
    };
}
