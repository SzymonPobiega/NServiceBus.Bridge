namespace NServiceBus.Bridge
{
    using System;
    using Routing;
    using Transport;
    using Persistence;

    /// <summary>
    /// Configures the switch port.
    /// </summary>
    /// <typeparam name="T">Type of transport.</typeparam>
    public class PortConfiguration<T>
        where T : TransportDefinition, new()
    {
        string Name;
        Action<TransportExtensions<T>> customization;
        Action<EndpointConfiguration> subscriptionPersistenceConfig;
        bool? autoCreateQueues;
        string autoCreateQueuesIdentity;
        int? maximumConcurrency;

        internal PortConfiguration(string name, Action<TransportExtensions<T>> customization)
        {
            Name = name;
            this.customization = customization;
        }

        /// <summary>
        /// Configures the port to use selected subscription persistence for message-driven publish/subscribe routing.
        /// </summary>
        /// <typeparam name="TPersistence">Type of persistence.</typeparam>
        /// <param name="subscriptionPersistenceConfiguration">A callback for configuring selected persistence.</param>
        public void UseSubscriptionPersistece<TPersistence>(Action<PersistenceExtensions<TPersistence>> subscriptionPersistenceConfiguration)
            where TPersistence : PersistenceDefinition
        {
            this.subscriptionPersistenceConfig = e =>
            {
                var persistence = e.UsePersistence<TPersistence>();
                subscriptionPersistenceConfiguration(persistence);
            };
        }

        /// <summary>
        /// Configures the port to automatically create a queue when starting up. Overrides switch-level setting.
        /// </summary>
        /// <param name="identity">Identity to use when creating the queue.</param>
        public void AutoCreateQueues(string identity = null)
        {
            autoCreateQueues = true;
            autoCreateQueuesIdentity = identity;
        }

        /// <summary>
        /// Limits the processing concurrency of the port to a given value.
        /// </summary>
        /// <param name="maximumConcurrency">Maximum level of concurrency for the port's transport.</param>
        public void LimitMessageProcessingConcurrencyTo(int maximumConcurrency)
        {
            this.maximumConcurrency = maximumConcurrency;
        }

        /// <summary>
        /// Distribution policy of the port.
        /// </summary>
        public RawDistributionPolicy DistributionPolicy { get; } = new RawDistributionPolicy();

        /// <summary>
        /// Physical routing settings of the port.
        /// </summary>
        public EndpointInstances EndpointInstances { get; } = new EndpointInstances();

        internal IPort Create(RuntimeTypeGenerator typeGenerator, string poisonQueue, bool? hubAutoCreateQueues, string hubAutoCreateQueuesIdentity, InterceptMessageForwarding interceptMethod, int immediateRetries, int delayedRetries, int circuitBreakerThreshold)
        {
            return new Port<T>(Name, customization, subscriptionPersistenceConfig, EndpointInstances, DistributionPolicy, typeGenerator, poisonQueue, maximumConcurrency, interceptMethod,
                autoCreateQueues ?? hubAutoCreateQueues ?? false, autoCreateQueuesIdentity ?? hubAutoCreateQueuesIdentity, immediateRetries, delayedRetries, circuitBreakerThreshold);
        }
    }
}
