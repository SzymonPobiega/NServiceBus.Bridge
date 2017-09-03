using System.Linq;
using NServiceBus;
using NServiceBus.Features;
using NServiceBus.Routing;
using NServiceBus.Routing.MessageDrivenSubscriptions;
using NServiceBus.Transport;

class BridgeRoutingFeature : Feature
{
    protected override void Setup(FeatureConfigurationContext context)
    {
        var settings = context.Settings;
        var transportInfra = settings.Get<TransportInfrastructure>();
        var nativePubSub = transportInfra.OutboundRoutingPolicy.Publishes == OutboundRoutingType.Multicast;
        var bridgeRoutingSettings = settings.Get<BridgeRoutingSettings>();
        var unicastRouteTable = settings.Get<UnicastRoutingTable>();
        var bridgeAddress = bridgeRoutingSettings.BridgeAddress;
        var route = UnicastRoute.CreateFromPhysicalAddress(bridgeAddress);
        var publishers = settings.Get<Publishers>();
        var bindings = settings.Get<QueueBindings>();

        //Make sure bridge queue does exist.
        bindings.BindSending(bridgeAddress);

        //Send the specified messages through the bridge
        var sendRouteTable = bridgeRoutingSettings.SendRouteTable;
        var routes = sendRouteTable.Select(x => new RouteTableEntry(x.Key, route)).ToList();
        unicastRouteTable.AddOrReplaceRoutes("NServiceBus.Bridge", routes);

        var distributorAddress = settings.GetOrDefault<string>("LegacyDistributor.Address");
        var subscriberAddress = distributorAddress ?? settings.LocalAddress();

        var publisherAddress = PublisherAddress.CreateFromPhysicalAddresses(bridgeAddress);
        var publisherTable = bridgeRoutingSettings.PublisherTable;
        publishers.AddOrReplacePublishers("Bridge", publisherTable.Select(kvp => new PublisherTableEntry(kvp.Key, publisherAddress)).ToList());

        var pipeline = context.Pipeline;
        var portTable = bridgeRoutingSettings.PortTable;
        pipeline.Register(new RoutingHeadersBehavior(sendRouteTable, portTable), "Sets the ultimate destination endpoint on the outgoing messages.");
        pipeline.Register(b => new BridgeSubscribeBehavior(subscriberAddress, settings.EndpointName(), bridgeAddress, b.Build<IDispatchMessages>(), publisherTable, portTable, nativePubSub),
            "Dispatches the subscribe request to the bridge.");
        pipeline.Register(b => new BridgeUnsubscribeBehavior(subscriberAddress, settings.EndpointName(), bridgeAddress, b.Build<IDispatchMessages>(), publisherTable, portTable, nativePubSub),
            "Dispatches the unsubscribe request to the bridge.");
    }
}
