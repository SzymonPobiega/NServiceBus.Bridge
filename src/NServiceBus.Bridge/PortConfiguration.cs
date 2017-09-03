﻿namespace NServiceBus.Bridge
{
    using System;
    using Routing;
    using Transport;
    using Persistence;

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

        public void UseSubscriptionPersistece<TPersistence>(Action<PersistenceExtensions<TPersistence>> subscriptionPersistenceConfiguration)
            where TPersistence : PersistenceDefinition
        {
            subscriptionPersistenceConfig = e =>
            {
                var persistence = e.UsePersistence<TPersistence>();
                subscriptionPersistenceConfiguration(persistence);
            };
        }

        public void AutoCreateQueues(string identity = null)
        {
            autoCreateQueues = true;
            autoCreateQueuesIdentity = identity;
        }

        public void LimitMessageProcessingConcurrencyTo(int maximumConcurrency)
        {
            this.maximumConcurrency = maximumConcurrency;
        }

        public DistributionPolicy DistributionPolicy { get; } = new DistributionPolicy();

        public EndpointInstances EndpointInstances { get; } = new EndpointInstances();

        internal IPort Create(RuntimeTypeGenerator typeGenerator, string poisonQueue, bool? hubAutoCreateQueues, string hubAutoCreateQueuesIdentity)
        {
            return new Port<T>(Name, customization, subscriptionPersistenceConfig, EndpointInstances, DistributionPolicy, typeGenerator, poisonQueue, maximumConcurrency,
                autoCreateQueues ?? hubAutoCreateQueues ?? false, autoCreateQueuesIdentity ?? hubAutoCreateQueuesIdentity);
        }
    }
}
