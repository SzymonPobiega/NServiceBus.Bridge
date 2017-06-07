using System;
using NServiceBus.Routing;
using NServiceBus.Transport;

namespace NServiceBus.Bridge
{
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

        internal BridgeConfiguration(string leftName, string rightName, Action<TransportExtensions<TLeft>> leftCustomization, Action<TransportExtensions<TRight>> rightCustomization)
        {
            this.LeftName = leftName;
            this.RightName = rightName;
            this.leftCustomization = leftCustomization;
            this.rightCustomization = rightCustomization;
        }

        public void AutoCreateQueues(string identity = null)
        {
            autoCreateQueues = true;
            autoCreateQueuesIdentity = identity;
        }

        public DistributionPolicy DistributionPolicy { get; } = new DistributionPolicy();

        public EndpointInstances EndpointInstances { get; } = new EndpointInstances();

        public IBridge Create()
        {
            return new Bridge<TLeft,TRight>(LeftName, RightName, autoCreateQueues, autoCreateQueuesIdentity, 
                EndpointInstances, new InMemorySubscriptionStorage(), DistributionPolicy, "poison",
                leftCustomization, rightCustomization);
        }
    }
}
