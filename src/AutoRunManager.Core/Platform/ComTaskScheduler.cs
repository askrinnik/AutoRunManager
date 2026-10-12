using System.Runtime.InteropServices;

namespace AutoRunManager.Core.Platform;

/// <summary>Task Scheduler 2.0 through its scripting COM API (<c>Schedule.Service</c>).</summary>
public sealed class ComTaskScheduler : ITaskScheduler
{
    private const int TaskEnumHidden = 1;
    private const int ActionExec = 0;
    private const int ActionComHandler = 5;

    public IReadOnlyList<ScheduledTaskInfo> GetTasks(Func<string, bool> includeFolder)
    {
        dynamic service = Connect();
        List<ScheduledTaskInfo> tasks = [];
        CollectFolder(service.GetFolder(@"\"), includeFolder, tasks);
        return tasks;
    }

    public void SetEnabled(string taskPath, bool enabled)
    {
        dynamic task = GetTask(taskPath);
        task.Enabled = enabled;
    }

    public string GetXml(string taskPath) => (string)GetTask(taskPath).Xml;

    public void Delete(string taskPath)
    {
        (string folder, string name) = Parsing.TaskNames.Split(taskPath);
        Connect().GetFolder(folder).DeleteTask(name, 0);
    }

    private static dynamic GetTask(string taskPath)
    {
        (string folder, string name) = Parsing.TaskNames.Split(taskPath);
        return Connect().GetFolder(folder).GetTask(name);
    }

    internal static dynamic Connect()
    {
        Type type = Type.GetTypeFromProgID("Schedule.Service", throwOnError: true)!;
        dynamic service = Activator.CreateInstance(type)!;
        service.Connect();
        return service;
    }

    private static void CollectFolder(dynamic folder, Func<string, bool> includeFolder, List<ScheduledTaskInfo> tasks)
    {
        string folderPath = folder.Path;
        if (!includeFolder(folderPath))
        {
            return;
        }

        foreach (dynamic task in folder.GetTasks(TaskEnumHidden))
        {
            if (TryRead(task) is { } info)
            {
                tasks.Add(info);
            }
        }

        foreach (dynamic subFolder in folder.GetFolders(0))
        {
            CollectFolder(subFolder, includeFolder, tasks);
        }
    }

    private static ScheduledTaskInfo? TryRead(dynamic task)
    {
        try
        {
            dynamic definition = task.Definition;
            List<TaskAction> actions = [];
            foreach (dynamic action in definition.Actions)
            {
                int type = action.Type;
                if (type == ActionExec)
                {
                    actions.Add(new TaskAction((string?)action.Path, (string?)action.Arguments, null));
                }
                else if (type == ActionComHandler)
                {
                    actions.Add(new TaskAction(null, null, (string?)action.ClassId));
                }
            }

            List<string> triggers = [];
            foreach (dynamic trigger in definition.Triggers)
            {
                int type = trigger.Type;
                triggers.Add(DescribeTrigger(type));
            }

            return new ScheduledTaskInfo(
                (string)task.Path,
                (string)task.Name,
                (bool)task.Enabled,
                actions,
                triggers,
                (string?)definition.Principal.UserId,
                (string?)definition.RegistrationInfo.Author,
                (string?)definition.RegistrationInfo.Description);
        }
        catch (COMException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    // TASK_TRIGGER_TYPE2
    private static string DescribeTrigger(int type) => type switch
    {
        0 => "On an event",
        1 => "One time",
        2 => "Daily",
        3 => "Weekly",
        4 or 5 => "Monthly",
        6 => "On idle",
        7 => "At task creation",
        8 => "At startup",
        9 => "At logon",
        11 => "On session change",
        12 => "Custom",
        _ => "Other",
    };
}
