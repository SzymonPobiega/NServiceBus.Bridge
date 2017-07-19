using NServiceBus.Configuration.AdvanceExtensibility;
using NServiceBus.Features;

namespace NServiceBus
{
    public static class BridgeRoutingExtensions
    {
        public static BridgeRoutingSettings ConnectToBridge(this RoutingSettings routingSettings, string bridgeAddress)
        {
            routingSettings.GetSettings().EnableFeatureByDefault(typeof(BridgeRoutingFeature));

            var settings = new BridgeRoutingSettings(bridgeAddress);
            routingSettings.GetSettings().Set<BridgeRoutingSettings>(settings);
            return settings;
        }
    }
}