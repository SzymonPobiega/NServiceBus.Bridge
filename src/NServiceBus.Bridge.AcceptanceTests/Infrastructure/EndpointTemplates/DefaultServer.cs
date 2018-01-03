namespace NServiceBus.AcceptanceTests.EndpointTemplates
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting.Customization;
    using AcceptanceTesting.Support;
    using Configuration.AdvancedExtensibility;
    using Features;
    using Transport;

    public class DefaultServer : IEndpointSetupTemplate
    {
        public DefaultServer()
        {
            typesToInclude = new List<Type>();
        }

        public DefaultServer(List<Type> typesToInclude)
        {
            this.typesToInclude = typesToInclude;
        }

#pragma warning disable CS0618
        public async Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, Action<EndpointConfiguration> configurationBuilderCustomization)
#pragma warning restore CS0618
        {
            var types = endpointConfiguration.GetTypesScopedByTestClass();

            typesToInclude.AddRange(types);

            var configuration = new EndpointConfiguration(endpointConfiguration.EndpointName);

            configuration.TypesToIncludeInScan(typesToInclude);
            configuration.EnableInstallers();

            configuration.DisableFeature<TimeoutManager>();

            var recoverability = configuration.Recoverability();
            recoverability.Delayed(delayed => delayed.NumberOfRetries(0));
            recoverability.Immediate(immediate => immediate.NumberOfRetries(0));
            configuration.SendFailedMessagesTo("error");
            configuration.UseSerialization<NewtonsoftSerializer>();

            configuration.RegisterComponentsAndInheritanceHierarchy(runDescriptor);

            await configuration.DefinePersistence(runDescriptor, endpointConfiguration).ConfigureAwait(false);

            configuration.GetSettings().SetDefault("ScaleOut.UseSingleBrokerQueue", true);
            configurationBuilderCustomization(configuration);

            //Hack to make other transports work when RabbitMQ is referenced
            configuration.GetSettings().Set("RabbitMQ.RoutingTopologySupportsDelayedDelivery", false);

            var transportDef = configuration.GetSettings().Get<TransportDefinition>();
            var queueBindings = configuration.GetSettings().Get<QueueBindings>();

            runDescriptor.OnTestCompleted(async summary =>
            {
                var allQueues = queueBindings.ReceivingAddresses.Concat(queueBindings.SendingAddresses);
                foreach (var queue in allQueues)
                {
                    await CleanUpHelper.CleanupQueue(summary, queue, transportDef.GetType());
                }
            });

            return configuration;
        }

        List<Type> typesToInclude;
    }
}