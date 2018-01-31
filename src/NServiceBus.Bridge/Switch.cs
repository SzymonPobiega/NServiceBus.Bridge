namespace NServiceBus.Bridge
{
    using System.Collections.Generic;
    using System.Linq;
    using Transport;

    /// <summary>
    /// Allows creating switches.
    /// </summary>
    public static class Switch
    {
        /// <summary>
        /// Creates a new instance of a switch based on the provided configuration.
        /// </summary>
        /// <param name="config">Switch configuration.</param>
        public static IBridge Create(SwitchConfiguration config)
        {
            var ports = config.PortFactories.Select(x => x()).ToArray();
            return new SwitchImpl(ports, (incomingPort, context) => ResolveDestinationPort(config.PortTable, context));
        }

        static string ResolveDestinationPort(Dictionary<string, string> routeTable, MessageContext context)
        {
            if (context.Headers.TryGetValue("NServiceBus.Bridge.DestinationPort", out var destinationPort))
            {
                return destinationPort;
            }
            string destinationEndpoint;
            if (!context.Headers.TryGetValue("NServiceBus.Bridge.DestinationEndpoint", out destinationEndpoint))
            {
                throw new UnforwardableMessageException("The message does not contain neither 'NServiceBus.Bridge.DestinationPort' header nor 'NServiceBus.Bridge.DestinationEndpoint' header.");
            }

            if (!routeTable.TryGetValue(destinationEndpoint, out destinationPort))
            {
                throw new UnforwardableMessageException($"The message does not contain 'NServiceBus.Bridge.DestinationPort' header and routing configuration does not have entry for endpoint '{destinationEndpoint}'.");
            }
            return destinationPort;
        }
    }
}