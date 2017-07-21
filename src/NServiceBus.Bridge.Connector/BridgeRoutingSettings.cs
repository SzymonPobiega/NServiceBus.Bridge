using System;
using System.Collections.Generic;

namespace NServiceBus
{
    public class BridgeRoutingSettings
    {
        internal BridgeRoutingSettings(string bridgeAddress)
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

        public void SetPort(string endpointName, string port)
        {
            if (string.IsNullOrEmpty(endpointName))
            {
                throw new ArgumentException("Endpoint name cannot be an empty.", nameof(endpointName));
            }

            if (string.IsNullOrEmpty(port))
            {
                throw new ArgumentException("Port name cannot be an empty.", nameof(port));
            }

            PortTable[endpointName] = port;
        }

        internal string BridgeAddress;
        internal Dictionary<Type, string> SendRouteTable = new Dictionary<Type, string>();
        internal Dictionary<Type, string> PublisherTable = new Dictionary<Type, string>();
        internal Dictionary<string, string> PortTable = new Dictionary<string, string>();
    }
}