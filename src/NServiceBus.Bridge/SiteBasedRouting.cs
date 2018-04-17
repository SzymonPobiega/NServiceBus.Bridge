namespace NServiceBus.Bridge
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Transport;

    /// <summary>
    /// Configures site-based routing between bridges.
    /// </summary>
    public static class SiteBasedRouting
    {
        /// <summary>
        /// Configures site-based routing between bridges.
        /// </summary>
        public static SiteRoutingConfiguration ConfigureSites<TLeft, TRight>(this BridgeConfiguration<TLeft, TRight> config) 
            where TLeft : TransportDefinition, new()
            where TRight : TransportDefinition, new()

        {
            var routingTable = new Dictionary<string, string>();
            config.Forwarding.RegisterRoutingCallback((context, type) =>
            {
                if (!context.Headers.TryGetValue("NServiceBus.Bridge.DestinationSites", out var sites))
                {
                    return null;
                }
                var siteArray = sites.Split(new[] {';'}, StringSplitOptions.RemoveEmptyEntries);
                var destinations = siteArray.Select(s =>
                {
                    if (!routingTable.TryGetValue(s, out var address))
                    {
                        throw new Exception($"Site {s} is not mapped in bridge forwarding configuration.");
                    }
                    return address;
                });

                return destinations.ToArray();
            });
            return new SiteRoutingConfiguration(routingTable);
        }
    }
}
