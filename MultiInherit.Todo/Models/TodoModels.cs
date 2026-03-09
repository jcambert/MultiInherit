namespace MultiInherit.Todo.Models;

// ═══════════════════════════════════════════════════════════════════════════
// todo.tag  — étiquette colorée pour classer les tâches (Many2many target)
// ═══════════════════════════════════════════════════════════════════════════

[Model("todo.tag", Description = "Tag")]
[SqlConstraint("uq_tag_label", "UNIQUE(Label)", "Tag label must be unique.")]
public partial class TodoTag
{
    [ModelField(String = "Label", Required = true)]
    public string Label { get; set; } = string.Empty;

    [ModelField(String = "Color", Help = "Hex color code, e.g. #4CAF50")]
    public string Color { get; set; } = "#607D8B";
}

// ═══════════════════════════════════════════════════════════════════════════
// todo.project  — conteneur logique de tâches
// ═══════════════════════════════════════════════════════════════════════════

[Model("todo.project", Description = "Project")]
[SqlConstraint("uq_project_name", "UNIQUE(Name)", "Project name must be unique.")]
public partial class TodoProject
{
    [ModelField(String = "Name", Required = true)]
    public string Name { get; set; } = string.Empty;

    [ModelField(String = "Description")]
    public string? Description { get; set; }

    [ModelField(String = "Color", Help = "Hex color code")]
    public string Color { get; set; } = "#1976D2";

    [ModelField(String = "Is Archived")]
    public bool IsArchived { get; set; }

    // One2many → todo.task (inverse de la Many2one Task.Project)
    [One2many("todo.task", "ProjectId", String = "Tasks")]
    public partial ICollection<TodoTask> Tasks { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// todo.task  — tâche avec hiérarchie, relations et champs calculés
// ═══════════════════════════════════════════════════════════════════════════

[Model("todo.task", Description = "Task")]
public partial class TodoTask
{
    [ModelField(String = "Title", Required = true)]
    public string Title { get; set; } = string.Empty;

    [ModelField(String = "Description")]
    public string? Description { get; set; }

    [ModelField(String = "Priority")]
    [Selection("low", "normal", "high", "urgent")]
    public string Priority { get; set; } = "normal";

    [ModelField(String = "Status")]
    [Selection("todo", "in_progress", "done", "cancelled")]
    public string Status { get; set; } = "todo";

    [ModelField(String = "Due Date")]
    public DateTime? DueDate { get; set; }

    [ModelField(String = "Completed At")]
    public DateTime? CompletedAt { get; set; }

    [ModelField(String = "Estimated Minutes", Help = "Estimated effort in minutes")]
    public int? EstimatedMinutes { get; set; }

    [ModelField(String = "Actual Minutes", Help = "Actual time spent in minutes")]
    public int? ActualMinutes { get; set; }

    // Many2one → todo.project (optionnel)
    [Many2one("todo.project", String = "Project", OnDelete = OnDeleteAction.SetNull)]
    public partial TodoProject? Project { get; set; }

    // Many2one → todo.task (auto-référence, optionnel)
    [Many2one("todo.task", String = "Parent Task", OnDelete = OnDeleteAction.SetNull)]
    public partial TodoTask? ParentTask { get; set; }

    // One2many → sous-tâches (inverse de ParentTask)
    [One2many("todo.task", "ParentTaskId", String = "Subtasks")]
    public partial ICollection<TodoTask> Subtasks { get; set; }

    // Many2many → todo.tag
    [Many2many("todo.tag", String = "Tags")]
    public partial ICollection<TodoTag> Tags { get; set; }

    // One2many → commentaires
    [One2many("todo.comment", "TaskId", String = "Comments")]
    public partial ICollection<TodoComment> Comments { get; set; }

    // ── Champs calculés (non stockés) ──────────────────────────────────────

    [Compute(nameof(_computeIsOverdue))]
    [Depends("DueDate", "Status")]
    public partial bool IsOverdue { get; private set; }

    private void _computeIsOverdue()
        => IsOverdue = DueDate.HasValue
            && DueDate.Value < new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, 0, 0, 0, DateTimeKind.Utc)
            && Status != "done"
            && Status != "cancelled";

    [Compute(nameof(_computeDisplayStatus))]
    [Depends("Status", "DueDate")]
    public partial string DisplayStatus { get; private set; }

    private void _computeDisplayStatus()
        => DisplayStatus = IsOverdue ? "overdue" : Status;

    // ── Contraintes ────────────────────────────────────────────────────────

    [Constrains("Title")]
    private void _checkTitle()
    {
        if (string.IsNullOrWhiteSpace(Title))
            throw new ModelValidationException("Task title cannot be empty.", nameof(Title));
    }

    [Constrains("EstimatedMinutes", "ActualMinutes")]
    private void _checkMinutes()
    {
        if (EstimatedMinutes.HasValue && EstimatedMinutes.Value < 0)
            throw new ModelValidationException("Estimated minutes cannot be negative.", nameof(EstimatedMinutes));
        if (ActualMinutes.HasValue && ActualMinutes.Value < 0)
            throw new ModelValidationException("Actual minutes cannot be negative.", nameof(ActualMinutes));
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// todo.comment  — commentaire associé à une tâche
// ═══════════════════════════════════════════════════════════════════════════

[Model("todo.comment", Description = "Task Comment")]
public partial class TodoComment
{
    [ModelField(String = "Content", Required = true)]
    public string Content { get; set; } = string.Empty;

    [ModelField(String = "Created At")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ModelField(String = "Author")]
    public string Author { get; set; } = "Me";

    // Many2one → todo.task (requis, suppression en cascade)
    [Many2one("todo.task", String = "Task", Required = true, OnDelete = OnDeleteAction.Cascade)]
    public partial TodoTask? Task { get; set; }

    [Constrains("Content")]
    private void _checkContent()
    {
        if (string.IsNullOrWhiteSpace(Content))
            throw new ModelValidationException("Comment content cannot be empty.", nameof(Content));
    }
}
