using System.Linq;
using NServiceBus;
using NServiceBus.Features;
using NServiceBus.Routing;
using NServiceBus.Routing.MessageDrivenSubscriptions;
using NServiceBus.Transport;

class RampFeature : Feature
{
    protected override void Setup(FeatureConfigurationContext context)
    {
        var rampSettings = context.Settings.Get<RampSettings>();
        var unicastRouteTable = context.Settings.Get<UnicastRoutingTable>();
        var route = UnicastRoute.CreateFromPhysicalAddress(rampSettings.BridgeAddress);
        var publishers = context.Settings.Get<Publishers>();

        //Send the specified messages through the bridge
        var routes = rampSettings.SendRouteTable.Select(x => new RouteTableEntry(x.Key, route)).ToList();
        unicastRouteTable.AddOrReplaceRoutes("NServiceBus.Bridge", routes);

        var distributorAddress = context.Settings.GetOrDefault<string>("LegacyDistributor.Address");
        var subscriberAddress = distributorAddress ?? context.Settings.LocalAddress();

        var publisherAddress = PublisherAddress.CreateFromPhysicalAddresses(rampSettings.BridgeAddress);
        publishers.AddOrReplacePublishers("Bridge", rampSettings.PublisherTable.Select(kvp => new PublisherTableEntry(kvp.Key, publisherAddress)).ToList());

        context.Pipeline.Register(new SetUltimateDestinationEndpointBehavior(rampSettings.SendRouteTable), "Sets the ultimate destination endpoint on the outgoing messages.");
        context.Pipeline.Register(new SetCorrelationIdBehavior(), "Encodes the reply-to address in the correlation ID.");
        context.Pipeline.Register(b => new BridgeSubscribeBehavior(subscriberAddress, context.Settings.EndpointName(), rampSettings.BridgeAddress, b.Build<IDispatchMessages>(), rampSettings.PublisherTable), 
            "Dispatches the subscribe request to the bridge.");
        context.Pipeline.Register(b => new BridgeUnsubscribeBehavior(subscriberAddress, context.Settings.EndpointName(), rampSettings.BridgeAddress, b.Build<IDispatchMessages>(), rampSettings.PublisherTable),
            "Dispatches the unsubscribe request to the bridge.");
    }
}
