namespace Samples.TransportBridge.BridgeHost
{
    using System;
    using System.ComponentModel;
    using System.Data.SqlClient;
    using System.ServiceProcess;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Bridge;
    using NServiceBus.Logging;
    using NServiceBus.Persistence.Sql;

    [DesignerCategory("Code")]
    class ProgramService : ServiceBase
    {
        private IBridge _bridge;
        static readonly ILog Logger = LogManager.GetLogger<ProgramService>();

        static void Main()
        {
            Console.Title = "Samples.TransportBridge.BridgeHost";

            using (var service = new ProgramService())
            {
                // to run interactive from a console or as a windows service
                if (Environment.UserInteractive)
                {
                    Console.CancelKeyPress += (sender, e) =>
                    {
                        service.OnStop();
                    };
                    service.OnStart(null);
                    Console.WriteLine("\r\nPress enter key to stop program\r\n");
                    Console.Read();
                    service.OnStop();
                    return;
                }

                Run(service);
            }
        }

        protected override void OnStart(string[] args)
        {
            AsyncOnStart().GetAwaiter().GetResult();
        }

        async Task AsyncOnStart()
        {
            try
            {
                var bridgeConfiguration = Bridge
                    .Between<MsmqTransport>(endpointName: "TransportBridge.MsmqBank")
                    .And<RabbitMQTransport>(
                        endpointName: "TransportBridge.RabbitBank",
                        customization: transportExtensions =>
                        {
                            transportExtensions.ConnectionString("host=localhost");                        
                        });

                bridgeConfiguration.UseSubscriptionPersistece<SqlPersistence>((e, c) =>
                    {
                        c.SqlVariant(SqlVariant.MsSqlServer);
                        c.ConnectionBuilder(() =>
                            new SqlConnection(
                                @"Data Source=.\SqlExpress;Initial Catalog=nsbBridge;Integrated Security=True"));
                        c.SubscriptionSettings().DisableCache();
                        e.EnableInstallers();
                    }
                );
            
                bridgeConfiguration.AutoCreateQueues();
                _bridge = bridgeConfiguration.Create();
                await _bridge.Start().ConfigureAwait(false);

                PerformStartupOperations();
            }
            catch (Exception exception)
            {
                Logger.Fatal("Failed to start", exception);
                Environment.FailFast("Failed to start", exception);
            }
        }

        void PerformStartupOperations()
        {
        }

        Task OnCriticalError(ICriticalErrorContext context)
        {
            //TODO: Decide if shutting down the process is the best response to a critical error
            // https://docs.particular.net/nservicebus/hosting/critical-errors
            var fatalMessage = $"The following critical error was encountered:\n{context.Error}\nProcess is shutting down.";
            Logger.Fatal(fatalMessage, context.Exception);
            Environment.FailFast(fatalMessage, context.Exception);
            return Task.FromResult(0);
        }

        protected override void OnStop()
        {
            _bridge?.Stop().GetAwaiter().GetResult();
        }
    }
}