using System;
using System.Collections.Generic;

namespace NServiceBus
{
    public class RampSettings
    {
        internal RampSettings(string bridgeAddress)
        {
            BridgeAddress = bridgeAddress;
        }

        public void RouteToEndpoint(Type messageType, string endpointName)
        {
            SendRouteTable[messageType] = endpointName;
        }

        public void RegisterPublisher(Type eventType, string publisherEndpointName)
        {
            PublisherTable[eventType] = publisherEndpointName;
        }

        internal string BridgeAddress;
        internal Dictionary<Type, string> SendRouteTable = new Dictionary<Type, string>();
        internal Dictionary<Type, string> PublisherTable = new Dictionary<Type, string>();
    }
}