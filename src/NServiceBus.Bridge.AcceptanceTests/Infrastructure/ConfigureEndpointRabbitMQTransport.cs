using System;
using System.Data.Common;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using RabbitMQ.Client;

static class ConfigureEndpointRabbitMQTransport
{
    public static TransportExtensions<RabbitMQTransport> Configure(this TransportExtensions<RabbitMQTransport> transport)
    {
        var connectionString = GetConnectionString();

        var connectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = connectionString };

        transport.ConnectionString(connectionStringBuilder.ConnectionString);
        transport.UseConventionalRoutingTopology();

        return transport;
    }

    static string GetConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString");
        if (string.IsNullOrEmpty(connectionString))
        {
            connectionString = "host=localhost";
        }
        return connectionString;
    }

    public static Task PurgeQueue(RunSummary runSummary, string queue)
    {
        if (!runSummary.RunDescriptor.Settings.TryGet(out ConnectionFactory connectionFactory))
        {
            connectionFactory = CreateConnectionFactory();
            runSummary.RunDescriptor.Settings.Set(connectionFactory);
        }

        using (var connection = connectionFactory.CreateConnection("Test Queue Purger"))
        using (var channel = connection.CreateModel())
        {
            try
            {
                channel.QueuePurge(queue);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to clear queue {0}: {1}", queue, ex);
            }
        }
        return Task.CompletedTask;
    }

    static ConnectionFactory CreateConnectionFactory()
    {
        var connectionString = GetConnectionString();

        var connectionStringBuilder = new DbConnectionStringBuilder
        {
            ConnectionString = connectionString
        };

        var connectionFactory = new ConnectionFactory
        {
            AutomaticRecoveryEnabled = true,
            UseBackgroundThreadsForIO = true
        };

        if (connectionStringBuilder.TryGetValue("username", out var value))
        {
            connectionFactory.UserName = value.ToString();
        }

        if (connectionStringBuilder.TryGetValue("password", out value))
        {
            connectionFactory.Password = value.ToString();
        }

        if (connectionStringBuilder.TryGetValue("virtualhost", out value))
        {
            connectionFactory.VirtualHost = value.ToString();
        }

        if (connectionStringBuilder.TryGetValue("host", out value))
        {
            connectionFactory.HostName = value.ToString();
        }
        else
        {
            throw new Exception("The connection string doesn't contain a value for 'host'.");
        }
        return connectionFactory;
    }
}