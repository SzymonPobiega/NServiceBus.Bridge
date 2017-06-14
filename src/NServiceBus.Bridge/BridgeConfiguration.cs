using System;
using NServiceBus.Routing;
using NServiceBus.Transport;

namespace NServiceBus.Bridge
{
    using Unicast.Subscriptions.MessageDrivenSubscriptions;

    public class BridgeConfiguration<TLeft, TRight>
        where TLeft : TransportDefinition, new()
        where TRight : TransportDefinition, new()
    {
        internal string LeftName;
        internal string RightName;
        Action<TransportExtensions<TLeft>> leftCustomization;
        Action<TransportExtensions<TRight>> rightCustomization;
        bool autoCreateQueues;
        string autoCreateQueuesIdentity;
        int? maximumConcurrency;
        ISubscriptionStorage subscriptionStorage;

        internal BridgeConfiguration(string leftName, string rightName, Action<TransportExtensions<TLeft>> leftCustomization, Action<TransportExtensions<TRight>> rightCustomization)
        {
            this.LeftName = leftName;
            this.RightName = rightName;
            this.leftCustomization = leftCustomization;
            this.rightCustomization = rightCustomization;
        }

        public void UseSubscriptionStorage(ISubscriptionStorage subscriptionStorage)
        {
            this.subscriptionStorage = subscriptionStorage;
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
            if (subscriptionStorage == null)
            {
                throw new Exception("Subscription storage has not been configured. Use `UseSubscriptionStorage` method to configure it. InMemorySubscriptionStorage can be used for development only (not suitable for production).");
            }

            return new Bridge<TLeft,TRight>(LeftName, RightName, autoCreateQueues, autoCreateQueuesIdentity, 
                EndpointInstances, subscriptionStorage, DistributionPolicy, "poison",
                leftCustomization, rightCustomization, maximumConcurrency);
        }
    }
}
