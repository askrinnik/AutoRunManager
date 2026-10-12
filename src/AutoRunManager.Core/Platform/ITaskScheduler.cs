namespace AutoRunManager.Core.Platform;

/// <summary>An action of a scheduled task: a program with arguments, or a COM handler.</summary>
public sealed record TaskAction(string? Program, string? Arguments, string? ComClassId);

/// <param name="Path">Full task path, e.g. <c>\Vendor\Updater</c>.</param>
/// <param name="Triggers">Readable trigger descriptions ("At logon", "At startup", ...).</param>
/// <param name="UserId">Principal the task runs as, as stored in the definition.</param>
public sealed record ScheduledTaskInfo(
    string Path,
    string Name,
    bool Enabled,
    IReadOnlyList<TaskAction> Actions,
    IReadOnlyList<string> Triggers,
    string? UserId,
    string? Author,
    string? Description);

public interface ITaskScheduler
{
    /// <summary>All tasks, including hidden ones, in folders accepted by <paramref name="includeFolder"/>.</summary>
    IReadOnlyList<ScheduledTaskInfo> GetTasks(Func<string, bool> includeFolder);
}
