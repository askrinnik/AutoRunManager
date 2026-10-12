using AutoRunManager.Core.Model;
using AutoRunManager.Core.Parsing;
using AutoRunManager.Core.Platform;

using Microsoft.Win32;

namespace AutoRunManager.Core.Sources;

/// <summary>Scheduled tasks outside the <c>\Microsoft\</c> folder.</summary>
public sealed class ScheduledTaskSource(ITaskScheduler scheduler, IRegistry registry, SystemContext context) : IEntrySource
{
    public string Name => "Task Scheduler";

    public IEnumerable<StartupEntry> Enumerate()
    {
        IReadOnlyList<ScheduledTaskInfo> tasks = scheduler.GetTasks(folder => !TaskNames.IsWindowsTask(folder.TrimEnd('\\') + @"\"));
        foreach (ScheduledTaskInfo task in tasks)
        {
            if (TaskNames.IsWindowsTask(task.Path))
            {
                continue;
            }

            (string command, string? executable) = DescribeAction(task.Actions.FirstOrDefault());
            yield return new StartupEntry(
                EntryKind.ScheduledTask,
                TaskNames.Clean(task.Name),
                command,
                executable,
                new ScheduledTaskLocator(task.Path),
                task.Enabled ? EntryState.Enabled : EntryState.Disabled,
                CanToggle: true,
                CanDelete: true,
                RequiresAdmin: !context.IsCurrentUser(task.UserId),
                Details: Describe(task));
        }
    }

    private (string Command, string? Executable) DescribeAction(TaskAction? action)
    {
        if (action is null)
        {
            return ("", null);
        }

        if (action.ComClassId is { } classId)
        {
            string? server = registry.GetString(RegistryHive.ClassesRoot, $@"CLSID\{classId}\InprocServer32", "");
            return ($"COM handler {classId}" + (server is null ? "" : $" ({server})"), CommandLine.GetExecutablePath(server));
        }

        string program = action.Program ?? "";
        string quoted = program.Contains(' ') && !program.StartsWith('"') ? $"\"{program}\"" : program;
        string command = string.IsNullOrWhiteSpace(action.Arguments) ? quoted : $"{quoted} {action.Arguments}";
        return (command, CommandLine.GetExecutablePath(quoted));
    }

    private static string Describe(ScheduledTaskInfo task)
    {
        List<string> parts = [$"Task: {task.Path}"];
        if (task.Triggers.Count > 0)
        {
            parts.Add("Triggers: " + string.Join(", ", task.Triggers.Distinct()));
        }

        if (!string.IsNullOrEmpty(task.UserId))
        {
            parts.Add("Runs as: " + task.UserId);
        }

        if (!string.IsNullOrWhiteSpace(task.Description))
        {
            parts.Add(task.Description.Trim());
        }

        return string.Join(Environment.NewLine, parts);
    }
}
