namespace NServiceBus.Bridge
{
    using System;
    using Routing;
    using Transport;
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
        InterceptMessageForwarding interceptMethod = (queue, message, forward) => forward();

        internal BridgeConfiguration(string leftName, string rightName, Action<TransportExtensions<TLeft>> leftCustomization, Action<TransportExtensions<TRight>> rightCustomization)
        {
            LeftName = leftName;
            RightName = rightName;
            this.leftCustomization = leftCustomization;
            this.rightCustomization = rightCustomization;
        }

        public void UseSubscriptionPersistece<TPersistence>(Action<EndpointConfiguration, PersistenceExtensions<TPersistence>> subscriptionPersistenceConfiguration)
            where TPersistence : PersistenceDefinition
        {
            this.subscriptionPersistenceConfig = e =>
            {
                var persistence = e.UsePersistence<TPersistence>();
                subscriptionPersistenceConfiguration(e, persistence);
            };
        }

        public void InterceptForawrding(InterceptMessageForwarding interceptMethod)
        {
            this.interceptMethod = interceptMethod ?? throw new ArgumentNullException(nameof(interceptMethod));
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

        public RuntimeTypeGenerator AzureServiceBusOnEndpointOrientedTopology { get; } = new RuntimeTypeGenerator();

        public DistributionPolicy DistributionPolicy { get; } = new DistributionPolicy();

        public EndpointInstances EndpointInstances { get; } = new EndpointInstances();

        public IBridge Create()
        {
            return new Bridge<TLeft,TRight>(LeftName, RightName, autoCreateQueues, autoCreateQueuesIdentity, 
                EndpointInstances, subscriptionPersistenceConfig, DistributionPolicy, "poison",
                leftCustomization, rightCustomization, maximumConcurrency, interceptMethod, AzureServiceBusOnEndpointOrientedTopology);
        }
    }
}
