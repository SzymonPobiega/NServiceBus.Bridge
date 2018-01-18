using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Features;
using NServiceBus.Transport;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

class PubSubInfrastructureBuilderFeature : Feature
{
    protected override void Setup(FeatureConfigurationContext context)
    {
        var transportInfra = context.Settings.Get<TransportInfrastructure>();
        if (transportInfra.OutboundRoutingPolicy.Publishes == OutboundRoutingType.Unicast)
        {
            context.RegisterStartupTask(b => new MessageDrivenPubSubInfrastructureBuilder(b.Build<PubSubInfrastructure>(), b.Build<ISubscriptionStorage>()));
        }
        else
        {
            throw new NotSupportedException("This feature should only be active when transport does not support native publish subscribe.");
        }
    }

    class MessageDrivenPubSubInfrastructureBuilder : FeatureStartupTask
    {
        PubSubInfrastructure pubSubInfrastructure;
        ISubscriptionStorage subscriptionStorage;

        public MessageDrivenPubSubInfrastructureBuilder(PubSubInfrastructure pubSubInfrastructure, ISubscriptionStorage subscriptionStorage)
        {
            this.pubSubInfrastructure = pubSubInfrastructure;
            this.subscriptionStorage = subscriptionStorage;
        }

        protected override Task OnStart(IMessageSession session)
        {
            var forwarder = new MessageDrivenSubscriptionForwarder(pubSubInfrastructure.EndpointInstances);
            var publishRouter = new MessageDrivenPublishRouter(subscriptionStorage, pubSubInfrastructure.DistributionPolicy);
            pubSubInfrastructure.Set(publishRouter, forwarder, subscriptionStorage);
            return Task.CompletedTask;
        }

        protected override Task OnStop(IMessageSession session)
        {
            return Task.CompletedTask;
        }
    }
}

