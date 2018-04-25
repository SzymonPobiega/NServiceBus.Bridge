namespace NServiceBus.Bridge
{
    using System.Collections.Generic;

    /// <summary>
    /// Configures site-based routing between bridges.
    /// </summary>
    public class SiteRoutingConfiguration
    {
        Dictionary<string, string> routeTable;

        internal SiteRoutingConfiguration(Dictionary<string, string> routeTable)
        {
            this.routeTable = routeTable;
        }

        /// <summary>
        /// Configures a remote site by setting its bridge address.
        /// </summary>
        /// <param name="siteName">Name of the site.</param>
        /// <param name="bridgeEndpoint">Name of this site's bridge endpoint.</param>
        public void AddSite(string siteName, string bridgeEndpoint)
        {
            routeTable[siteName] = bridgeEndpoint;
        }
    }
}