namespace MultiInherit.Todo.Services;

/// <summary>
/// Bus d'événements singleton pour la communication entre le service background
/// et les composants Blazor en temps réel.
/// </summary>
public class TodoEventService
{
    /// <summary>Déclenché quand des tâches en retard sont détectées.</summary>
    public event Func<OverdueDetectedArgs, Task>? OnOverdueDetected;

    /// <summary>Déclenché quand une tâche est modifiée (création, update, suppression).</summary>
    public event Func<TaskChangedArgs, Task>? OnTaskChanged;

    public async Task RaiseOverdueDetectedAsync(OverdueDetectedArgs args)
    {
        if (OnOverdueDetected != null)
            await OnOverdueDetected.Invoke(args);
    }

    public async Task RaiseTaskChangedAsync(TaskChangedArgs args)
    {
        if (OnTaskChanged != null)
            await OnTaskChanged.Invoke(args);
    }
}

public record OverdueDetectedArgs(int Count, DateTime DetectedAt);
public record TaskChangedArgs(int TaskId, string ChangeType);  // "created" | "updated" | "deleted"
