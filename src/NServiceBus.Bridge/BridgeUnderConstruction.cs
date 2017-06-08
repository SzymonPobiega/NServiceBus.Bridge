using System;
using NServiceBus.Transport;

namespace NServiceBus.Bridge
{
    public static class Bridge
    {
        public static BridgeUnderConstruction<TLeft> Between<TLeft>(string endpointName, Action<TransportExtensions<TLeft>> customization = null)
            where TLeft : TransportDefinition, new()
        {
            return new BridgeUnderConstruction<TLeft>(endpointName, customization);
        }
    }

    public class BridgeUnderConstruction<TLeft>
        where TLeft : TransportDefinition, new()
    {
        string leftName;
        Action<TransportExtensions<TLeft>> leftCustomization;

        public BridgeUnderConstruction(string leftName, Action<TransportExtensions<TLeft>> leftCustomization)
        {
            this.leftName = leftName;
            this.leftCustomization = leftCustomization;
        }

        public BridgeConfiguration<TLeft, TRight> And<TRight>(string endpointName,
            Action<TransportExtensions<TRight>> customization = null)
            where TRight : TransportDefinition, new()
        {
            return new BridgeConfiguration<TLeft, TRight>(leftName, endpointName, leftCustomization, customization);
        }
    }
}