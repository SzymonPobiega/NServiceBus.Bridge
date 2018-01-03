using System;
using System.Collections.Generic;
using System.Linq;
using System.Messaging;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;

public static class ConfigureEndpointMsmqTransport
{
    public static TransportExtensions<MsmqTransport> Configure(this TransportExtensions<MsmqTransport> transportConfig)
    {
        transportConfig.DisableConnectionCachingForSends();
        return transportConfig;
    }

    public static Task DeleteQueue(RunSummary summary, string queue)
    {
        if (!summary.RunDescriptor.Settings.TryGet(out MessageQueue[] allQueues))
        {
            allQueues = MessageQueue.GetPrivateQueuesByMachine("localhost");
            summary.RunDescriptor.Settings.Set(allQueues);
        }
        var queuesToBeDeleted = new List<string>();

        foreach (var messageQueue in allQueues)
        {
            using (messageQueue)
            {
                var localQueueName = queue.Split(new[]{ '@' }, StringSplitOptions.RemoveEmptyEntries).First();
                if (messageQueue.QueueName.StartsWith(@"private$\" + localQueueName, StringComparison.OrdinalIgnoreCase))
                {
                    queuesToBeDeleted.Add(messageQueue.Path);
                }
            }
        }

        foreach (var queuePath in queuesToBeDeleted)
        {
            try
            {
                MessageQueue.Delete(queuePath);
                Console.WriteLine("Deleted '{0}' queue", queuePath);
            }
            catch (Exception)
            {
                Console.WriteLine("Could not delete queue '{0}'", queuePath);
            }
        }

        MessageQueue.ClearConnectionCache();

        return Task.FromResult(0);
    }
}