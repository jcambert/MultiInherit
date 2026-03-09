using MultiInherit.Todo.Models;

namespace MultiInherit.Todo.Services;

// ── DTOs ─────────────────────────────────────────────────────────────────

public record TaskFilter(
    int?   ProjectId       = null,
    string? Status         = null,
    string? Priority       = null,
    int?   TagId           = null,
    string? Search         = null,
    bool   ShowCompleted   = true,
    bool   ShowCancelled   = false,
    bool   OverdueOnly     = false,
    bool   NoProjectOnly   = false);

public record TaskUpsertDto(
    string   Title,
    string?  Description,
    string   Priority,
    string   Status,
    DateTime? DueDate,
    int?     ProjectId,
    int?     ParentTaskId,
    int?     EstimatedMinutes,
    List<int> TagIds);

public record TaskStats(
    int  Total,
    int  Overdue,
    int  DueToday,
    int  Completed,
    int  InProgress,
    int  Todo,
    int  Cancelled,
    Dictionary<string, int> ByPriority,
    Dictionary<string, int> ByProject);

// ── Interface ─────────────────────────────────────────────────────────────

public interface ITodoService
{
    // Tasks
    Task<List<TodoTask>> GetTasksAsync(TaskFilter? filter = null);
    Task<TodoTask?>      GetTaskByIdAsync(int id);
    Task<TodoTask>       CreateTaskAsync(TaskUpsertDto dto);
    Task<TodoTask>       UpdateTaskAsync(int id, TaskUpsertDto dto);
    Task                 CompleteTaskAsync(int id);
    Task                 ReopenTaskAsync(int id);
    Task                 DeleteTaskAsync(int id);
    Task<int>            GetOverdueCountAsync();

    // Projects
    Task<List<TodoProject>>           GetProjectsAsync(bool includeArchived = false);
    Task<TodoProject>                 CreateProjectAsync(string name, string? description, string color);
    Task<TodoProject>                 UpdateProjectAsync(int id, string name, string? description, string color);
    Task                              ArchiveProjectAsync(int id, bool archive);
    Task                              DeleteProjectAsync(int id);
    Task<Dictionary<int, int>>        GetTaskCountByProjectAsync();

    // Tags
    Task<List<TodoTag>> GetTagsAsync();
    Task<TodoTag>       CreateTagAsync(string label, string color);
    Task<TodoTag>       UpdateTagAsync(int id, string label, string color);
    Task                DeleteTagAsync(int id);

    // Comments
    Task<List<TodoComment>> GetCommentsAsync(int taskId);
    Task<TodoComment>       AddCommentAsync(int taskId, string content, string author = "Me");
    Task                    DeleteCommentAsync(int id);

    // Stats
    Task<TaskStats> GetStatsAsync();
}
