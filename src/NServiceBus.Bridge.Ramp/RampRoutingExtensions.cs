using NServiceBus.Configuration.AdvanceExtensibility;
using NServiceBus.Features;

namespace NServiceBus
{
    public static class RampRoutingExtensions
    {
        public static RampSettings UseBridgeRamp(this RoutingSettings routingSettings, string bridgeAddress)
        {
            routingSettings.GetSettings().EnableFeatureByDefault(typeof(RampFeature));

            var rampSettings = new RampSettings(bridgeAddress);
            routingSettings.GetSettings().Set<RampSettings>(rampSettings);
            return rampSettings;
        }
    }
}