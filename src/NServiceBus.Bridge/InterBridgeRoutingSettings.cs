using System.Collections.Generic;

namespace NServiceBus.Bridge
{
    /// <summary>
    /// Configures the connection to the bridge.
    /// </summary>
    public class InterBridgeRoutingSettings
    {
        internal InterBridgeRoutingSettings()
        {
        }

        /// <summary>
        /// Instructs the endpoint to route messages of this type to a designated endpoint on the other side of the bridge.
        /// </summary>
        /// <param name="messageType">Message type.</param>
        /// <param name="nextHop">Name of the next hop.</param>
        public void ForwardTo(string messageType, string nextHop)
        {
            SendRouteTable[messageType] = nextHop;
        }

        /// <summary>
        /// Registers a designated endpoint as a publisher of the events of this type. The endpoint will be used as a destination of subscribe messages.
        /// </summary>
        /// <param name="eventType">Type of the event.</param>
        /// <param name="nextHop">Name of the next hop.</param>
        public void RegisterPublisher(string eventType, string nextHop)
        {
            PublisherTable[eventType] = nextHop;
        }

        internal Dictionary<string, string> SendRouteTable = new Dictionary<string, string>();
        internal Dictionary<string, string> PublisherTable = new Dictionary<string, string>();
    }
}