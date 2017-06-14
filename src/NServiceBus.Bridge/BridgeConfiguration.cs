﻿using System;
using NServiceBus.Routing;
using NServiceBus.Transport;

namespace NServiceBus.Bridge
{
    using Persistence;

    public class BridgeConfiguration<TLeft, TRight>
        where TLeft : TransportDefinition, new()
        where TRight : TransportDefinition, new()
    {
        internal string LeftName;
        internal string RightName;
        Action<TransportExtensions<TLeft>> leftCustomization;
        Action<TransportExtensions<TRight>> rightCustomization;
        Action<EndpointConfiguration> subscriptionPersistenceConfig;
        bool autoCreateQueues;
        string autoCreateQueuesIdentity;
        int? maximumConcurrency;

        internal BridgeConfiguration(string leftName, string rightName, Action<TransportExtensions<TLeft>> leftCustomization, Action<TransportExtensions<TRight>> rightCustomization)
        {
            LeftName = leftName;
            RightName = rightName;
            this.leftCustomization = leftCustomization;
            this.rightCustomization = rightCustomization;
        }

        public void UseSubscriptionPersistece<TPersistence>(Action<PersistenceExtensions<TPersistence>> subscriptionPersistenceConfiguration)
            where TPersistence : PersistenceDefinition
        {
            this.subscriptionPersistenceConfig = e =>
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

        public IBridge Create()
        {
            return new Bridge<TLeft,TRight>(LeftName, RightName, autoCreateQueues, autoCreateQueuesIdentity, 
                EndpointInstances, subscriptionPersistenceConfig, DistributionPolicy, "poison",
                leftCustomization, rightCustomization, maximumConcurrency);
        }
    }
}
