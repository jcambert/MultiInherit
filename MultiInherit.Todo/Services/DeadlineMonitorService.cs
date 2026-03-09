using Microsoft.EntityFrameworkCore;
using MultiInherit.Todo.Data;
using MultiInherit.Todo.Models;

namespace MultiInherit.Todo.Services;

/// <summary>
/// Service hébergé qui surveille les tâches en retard toutes les N secondes
/// et notifie l'interface via <see cref="TodoEventService"/>.
/// </summary>
public sealed class DeadlineMonitorService(
    IDbContextFactory<TodoDbContext> factory,
    TodoEventService                 eventService,
    IConfiguration                  config,
    ILogger<DeadlineMonitorService>  logger) : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(
        config.GetValue<int>("DeadlineMonitor:IntervalSeconds", 60));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "DeadlineMonitorService démarré — interval : {Interval}s", _interval.TotalSeconds);

        // Attendre 5 secondes au démarrage pour laisser le temps à l'app d'initialiser
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckDeadlinesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erreur dans DeadlineMonitorService");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        logger.LogInformation("DeadlineMonitorService arrêté.");
    }

    private async Task CheckDeadlinesAsync(CancellationToken ct)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);

        var now   = DateTime.UtcNow.Date;
        var tasks = await ctx.Set<TodoTask>()
            .Where(t => t.DueDate.HasValue
                     && t.DueDate.Value < now
                     && t.Status != "done"
                     && t.Status != "cancelled")
            .ToListAsync(ct);

        if (tasks.Count == 0) return;

        // Journaliser les tâches en retard
        logger.LogWarning(
            "{Count} tâche(s) en retard détectée(s) à {Time}",
            tasks.Count,
            DateTime.UtcNow.ToString("HH:mm:ss"));

        foreach (var t in tasks)
        {
            logger.LogDebug(
                "  → [#{Id}] {Title} — échéance : {Due}",
                t.Id, t.Title, t.DueDate!.Value.ToString("dd/MM/yyyy"));
        }

        // Notifier les composants Blazor
        await eventService.RaiseOverdueDetectedAsync(
            new OverdueDetectedArgs(tasks.Count, DateTime.UtcNow));

        // Tâches dont l'échéance est dans les prochaines 24 h (alerte préventive)
        var tomorrow  = now.AddDays(1);
        var dueSoon   = await ctx.Set<TodoTask>()
            .Where(t => t.DueDate.HasValue
                     && t.DueDate.Value.Date == tomorrow
                     && t.Status != "done"
                     && t.Status != "cancelled")
            .CountAsync(ct);

        if (dueSoon > 0)
        {
            logger.LogInformation(
                "{Count} tâche(s) arrivent à échéance demain.", dueSoon);
        }
    }
}
