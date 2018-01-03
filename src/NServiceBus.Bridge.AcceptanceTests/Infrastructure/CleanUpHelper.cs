using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;

static class CleanUpHelper
{
    public static Task CleanupQueue(RunSummary summary, string queue, Type transport)
    {
        if (transport == typeof(MsmqTransport))
        {
            return ConfigureEndpointMsmqTransport.DeleteQueue(summary, queue);
        }
        if (transport == typeof(RabbitMQTransport))
        {
            return ConfigureEndpointRabbitMQTransport.PurgeQueue(summary, queue);
        }
        throw new Exception("Unsupported transport");
    }
}