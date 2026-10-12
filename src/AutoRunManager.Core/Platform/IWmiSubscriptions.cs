using System.Management;

namespace AutoRunManager.Core.Platform;

/// <summary>A permanent event subscription in <c>root\subscription</c> whose consumer runs code.</summary>
/// <param name="ConsumerClass"><c>CommandLineEventConsumer</c> or <c>ActiveScriptEventConsumer</c>.</param>
/// <param name="Command">Command line, script file, or the script text.</param>
/// <param name="Query">WQL query of the event filter.</param>
public sealed record WmiSubscriptionInfo(
    string ConsumerName,
    string ConsumerClass,
    string Command,
    string? Query,
    string FilterPath,
    string ConsumerPath,
    string BindingPath);

public interface IWmiSubscriptions
{
    IReadOnlyList<WmiSubscriptionInfo> GetSubscriptions();

    /// <summary>Deletes the binding, then the consumer and the filter (needs admin rights).</summary>
    void Delete(string bindingPath, string consumerPath, string filterPath);
}

public sealed class WmiSubscriptions : IWmiSubscriptions
{
    private const string Namespace = @"root\subscription";

    public IReadOnlyList<WmiSubscriptionInfo> GetSubscriptions()
    {
        List<WmiSubscriptionInfo> result = [];
        ManagementScope scope = new(Namespace);
        using ManagementObjectSearcher bindings = new(scope, new ObjectQuery("SELECT * FROM __FilterToConsumerBinding"));
        foreach (ManagementObject binding in bindings.Get().Cast<ManagementObject>())
        {
            using (binding)
            {
                string consumerPath = (string)binding["Consumer"];
                string filterPath = (string)binding["Filter"];
                using ManagementObject consumer = new(scope, new ManagementPath(consumerPath), null);
                consumer.Get();
                string consumerClass = consumer.ClassPath.ClassName;
                string? command = consumerClass switch
                {
                    "CommandLineEventConsumer" => (consumer["CommandLineTemplate"] as string) ?? (consumer["ExecutablePath"] as string),
                    "ActiveScriptEventConsumer" => (consumer["ScriptFileName"] as string) ?? (consumer["ScriptText"] as string),
                    _ => null,
                };
                if (command is null)
                {
                    continue;
                }

                string? query = null;
                try
                {
                    using ManagementObject filter = new(scope, new ManagementPath(filterPath), null);
                    filter.Get();
                    query = filter["Query"] as string;
                }
                catch (ManagementException)
                {
                }

                result.Add(new WmiSubscriptionInfo(
                    (consumer["Name"] as string) ?? consumerPath,
                    consumerClass,
                    command,
                    query,
                    filterPath,
                    consumerPath,
                    binding.Path.RelativePath));
            }
        }

        return result;
    }

    public void Delete(string bindingPath, string consumerPath, string filterPath)
    {
        ManagementScope scope = new(Namespace);
        foreach (string path in (string[])[bindingPath, consumerPath, filterPath])
        {
            using ManagementObject instance = new(scope, new ManagementPath(path), null);
            instance.Delete();
        }
    }
}
