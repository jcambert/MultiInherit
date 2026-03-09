using Microsoft.EntityFrameworkCore;
using MultiInherit.EFCore;
using MultiInherit.Todo.Models;

namespace MultiInherit.Todo.Data;

/// <summary>
/// DbContext spécialisé pour l'application TodoList.
/// Hérite de ModelDbContext qui auto-mappe tous les modèles enregistrés.
/// </summary>
public class TodoDbContext(DbContextOptions<TodoDbContext> options)
    : ModelDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Index de performance : recherche par statut, priorité, projet
        builder.Entity<TodoTask>()
            .HasIndex(t => t.Status)
            .HasDatabaseName("idx_todo_task_status");

        builder.Entity<TodoTask>()
            .HasIndex(t => t.Priority)
            .HasDatabaseName("idx_todo_task_priority");

        builder.Entity<TodoTask>()
            .HasIndex(t => t.DueDate)
            .HasDatabaseName("idx_todo_task_due_date");

        builder.Entity<TodoComment>()
            .HasIndex(t => t.CreatedAt)
            .HasDatabaseName("idx_todo_comment_created_at");
    }
}

/// <summary>
/// Peuple la base avec des données de démonstration au premier démarrage.
/// </summary>
public static class TodoSeeder
{
    public static async Task SeedAsync(TodoDbContext ctx)
    {
        // Ne seeder qu'une seule fois
        if (await ctx.Set<TodoProject>().AnyAsync()) return;

        // ── Projets ──────────────────────────────────────────────────────
        var devProject = new TodoProject { Name = "Développement", Color = "#1565C0", Description = "Tâches de développement logiciel" };
        var designProject = new TodoProject { Name = "Design", Color = "#6A1B9A", Description = "Maquettes et assets graphiques" };
        var opsProject = new TodoProject { Name = "Opérations", Color = "#2E7D32", Description = "Infrastructure et déploiement" };
        ctx.Set<TodoProject>().AddRange(devProject, designProject, opsProject);

        // ── Tags ─────────────────────────────────────────────────────────
        var bugTag = new TodoTag { Label = "Bug", Color = "#F44336" };
        var featureTag = new TodoTag { Label = "Feature", Color = "#4CAF50" };
        var docsTag = new TodoTag { Label = "Docs", Color = "#FF9800" };
        var reviewTag = new TodoTag { Label = "Review", Color = "#9C27B0" };
        var blockedTag = new TodoTag { Label = "Blocked", Color = "#607D8B" };
        ctx.Set<TodoTag>().AddRange(bugTag, featureTag, docsTag, reviewTag, blockedTag);

        await ctx.SaveChangesAsync();

        // ── Tâches ────────────────────────────────────────────────────────
        var t1 = new TodoTask
        {
            Title = "Implémenter l'authentification JWT",
            Description = "Ajouter la gestion des tokens JWT pour l'API REST.",
            Priority = "high",
            Status = "in_progress",
            DueDate = DateTime.Today.AddDays(3),
            ProjectId = devProject.Id,
            EstimatedMinutes = 240
        };
        var t2 = new TodoTask
        {
            Title = "Corriger le bug de pagination",
            Description = "La pagination saute des enregistrements sur la page 3.",
            Priority = "urgent",
            Status = "todo",
            DueDate = DateTime.Today.AddDays(-1), // Overdue !
            ProjectId = devProject.Id,
            EstimatedMinutes = 60
        };
        var t3 = new TodoTask
        {
            Title = "Maquette du dashboard",
            Description = "Créer les wireframes du nouveau dashboard analytique.",
            Priority = "normal",
            Status = "done",
            DueDate = DateTime.Today.AddDays(-5),
            CompletedAt = DateTime.Today.AddDays(-2),
            ProjectId = designProject.Id,
            EstimatedMinutes = 180,
            ActualMinutes = 200
        };
        var t4 = new TodoTask
        {
            Title = "Configurer le pipeline CI/CD",
            Description = "GitHub Actions pour build, test et deploy automatique.",
            Priority = "high",
            Status = "todo",
            DueDate = DateTime.Today.AddDays(7),
            ProjectId = opsProject.Id,
            EstimatedMinutes = 300
        };
        var t5 = new TodoTask
        {
            Title = "Rédiger la documentation API",
            Description = "OpenAPI/Swagger pour tous les endpoints publics.",
            Priority = "low",
            Status = "todo",
            DueDate = DateTime.Today.AddDays(14),
            ProjectId = devProject.Id,
            EstimatedMinutes = 480
        };
        var t6 = new TodoTask
        {
            Title = "Optimiser les requêtes N+1",
            Description = "Utiliser Include() et projection pour éviter les requêtes N+1 dans les listes.",
            Priority = "normal",
            Status = "todo",
            DueDate = DateTime.Today.AddDays(5),
            ProjectId = devProject.Id,
            EstimatedMinutes = 120
        };
        ctx.Set<TodoTask>().AddRange(t1, t2, t3, t4, t5, t6);
        await ctx.SaveChangesAsync();

        // ── Sous-tâches ───────────────────────────────────────────────────
        var sub1 = new TodoTask
        {
            Title = "Créer le middleware d'authentification",
            Priority = "high",
            Status = "done",
            ParentTaskId = t1.Id,
            ProjectId = devProject.Id,
            CompletedAt = DateTime.Now.AddHours(-2)
        };
        var sub2 = new TodoTask
        {
            Title = "Écrire les tests unitaires JWT",
            Priority = "high",
            Status = "in_progress",
            ParentTaskId = t1.Id,
            ProjectId = devProject.Id,
            EstimatedMinutes = 90
        };
        var sub3 = new TodoTask
        {
            Title = "Documenter l'endpoint /auth/token",
            Priority = "normal",
            Status = "todo",
            ParentTaskId = t1.Id,
            ProjectId = devProject.Id,
            EstimatedMinutes = 30
        };
        ctx.Set<TodoTask>().AddRange(sub1, sub2, sub3);
        await ctx.SaveChangesAsync();

        // ── Tags M2M ──────────────────────────────────────────────────────
        // Chargement avec navigation pour assigner les tags
        var taskWithTags1 = await ctx.Set<TodoTask>().Include(t => t.Tags).FirstAsync(t => t.Id == t1.Id);
        taskWithTags1.Tags.Add(featureTag);
        taskWithTags1.Tags.Add(reviewTag);

        var taskWithTags2 = await ctx.Set<TodoTask>().Include(t => t.Tags).FirstAsync(t => t.Id == t2.Id);
        taskWithTags2.Tags.Add(bugTag);
        taskWithTags2.Tags.Add(blockedTag);

        var taskWithTags5 = await ctx.Set<TodoTask>().Include(t => t.Tags).FirstAsync(t => t.Id == t5.Id);
        taskWithTags5.Tags.Add(docsTag);
        taskWithTags5.Tags.Add(featureTag);

        await ctx.SaveChangesAsync();

        // ── Commentaires ──────────────────────────────────────────────────
        ctx.Set<TodoComment>().AddRange(
            new TodoComment { TaskId = t1.Id, Content = "Le middleware est en place, reste les tests.", Author = "Alice", CreatedAt = DateTime.UtcNow.AddHours(-3) },
            new TodoComment { TaskId = t1.Id, Content = "J'ai trouvé un edge case avec les tokens expirés.", Author = "Bob", CreatedAt = DateTime.UtcNow.AddHours(-1) },
            new TodoComment { TaskId = t2.Id, Content = "Le bug se reproduit avec des données > 100 éléments.", Author = "Alice", CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new TodoComment { TaskId = t4.Id, Content = "On utilise GitHub Actions ou GitLab CI ?", Author = "Charlie", CreatedAt = DateTime.UtcNow.AddHours(-5) }
        );
        await ctx.SaveChangesAsync();
    }
}
