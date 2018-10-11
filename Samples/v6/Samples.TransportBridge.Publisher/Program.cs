namespace Samples.TransportBridge.Publisher
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Persistence.Sql;
    using Shared;

    static class Program
    {

        static void Main()
        {
            AsyncMain().GetAwaiter().GetResult();
        }

        static async Task AsyncMain()
        {
            Console.Title = "Samples.TransportBridge.Publisher";
            var endpointConfiguration = new EndpointConfiguration("Samples.TransportBridge.Publisher");

            var persistence = endpointConfiguration.UsePersistence<SqlPersistence>();
            var connection = @"Data Source=.\SqlExpress;Initial Catalog=nsbBridge;Integrated Security=True";
            persistence.SqlVariant(SqlVariant.MsSqlServer);
            persistence.ConnectionBuilder(
                connectionBuilder: () =>
                {
                    return new SqlConnection(connection);
                });

            var transport = endpointConfiguration.UseTransport<RabbitMQTransport>();
            transport.ConnectionString("host=localhost");

            endpointConfiguration.SendFailedMessagesTo("error");
            endpointConfiguration.EnableInstallers();

            var endpointInstance = await Endpoint.Start(endpointConfiguration).ConfigureAwait(false);

            await Start(endpointInstance).ConfigureAwait(false);

            await endpointInstance.Stop().ConfigureAwait(false);
        }

        static async Task Start(IEndpointInstance endpointInstance)
        {
            Console.WriteLine("Press '1' to publish the OrderReceived event");
            Console.WriteLine("Press any other key to exit");

            #region PublishLoop

            while (true)
            {
                var key = Console.ReadKey();
                Console.WriteLine();

                var orderReceivedId = Guid.NewGuid();
                if (key.Key == ConsoleKey.D1)
                {
                    var orderReceived = new OrderReceived
                    {
                        OrderId = orderReceivedId
                    };
                    await endpointInstance.Publish(orderReceived)
                        .ConfigureAwait(false);
                    Console.WriteLine($"Published OrderReceived Event with Id {orderReceivedId}.");
                }
                else
                {
                    return;
                }
            }

            #endregion
        }
    }
}