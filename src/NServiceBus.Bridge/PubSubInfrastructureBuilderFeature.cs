using System;
using System.Reflection;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Features;
using NServiceBus.Transport;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

class PubSubInfrastructureBuilderFeature : Feature
{
    public PubSubInfrastructureBuilderFeature()
    {
        Defaults(s =>
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.CreateInstance;
            var parameters = new[]
            {
                typeof(LogicalAddress),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(TransportTransactionMode),
                typeof(PushRuntimeSettings),
                typeof(bool)
            };
            var ctor = typeof(Endpoint).Assembly.GetType("NServiceBus.ReceiveConfiguration", true).GetConstructor(flags, null, parameters, null);

            var receiveConfig = ctor.Invoke(new object[]{null, s.EndpointName(),null, null, null, null, false});
            s.Set("NServiceBus.ReceiveConfiguration", receiveConfig);
        });
    }

    protected override void Setup(FeatureConfigurationContext context)
    {
        var transportInfra = context.Settings.Get<TransportInfrastructure>();
        if (transportInfra.OutboundRoutingPolicy.Publishes == OutboundRoutingType.Unicast)
        {
            context.RegisterStartupTask(b => new MessageDrivenPubSubInfrastructureBuilder(b.Build<PubSubInfrastructure>(), b.Build<ISubscriptionStorage>()));
        }
        else
        {
            CreateSubscriptionManager(transportInfra);
            context.RegisterStartupTask(b => new NativePubSubInfrastructureBuilder(b.Build<PubSubInfrastructure>(), CreateSubscriptionManager(transportInfra)));
        }
    }

    static IManageSubscriptions CreateSubscriptionManager(TransportInfrastructure transportInfra)
    {
        var subscriptionInfra = transportInfra.ConfigureSubscriptionInfrastructure();
        var factoryProperty = typeof(TransportSubscriptionInfrastructure).GetProperty("SubscriptionManagerFactory", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var factoryInstance = (Func<IManageSubscriptions>) factoryProperty.GetValue(subscriptionInfra, new object[0]);
        return factoryInstance();
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

    class NativePubSubInfrastructureBuilder : FeatureStartupTask
    {
        PubSubInfrastructure pubSubInfrastructure;
        IManageSubscriptions subscriptionManager;

        public NativePubSubInfrastructureBuilder(PubSubInfrastructure pubSubInfrastructure, IManageSubscriptions subscriptionManager)
        {
            this.pubSubInfrastructure = pubSubInfrastructure;
            this.subscriptionManager = subscriptionManager;
        }

        protected override Task OnStart(IMessageSession session)
        {
            var typeGenerator = new RuntimeTypeGenerator();
            var forwarder = new NativeSubscriptionForwarder(subscriptionManager, typeGenerator);
            var publishRouter = new NativePublishRouter(typeGenerator);
            pubSubInfrastructure.Set(publishRouter, forwarder, new NativeSubscriptionStorage());
            return Task.CompletedTask;
        }

        protected override Task OnStop(IMessageSession session)
        {
            return Task.CompletedTask;
        }
    }
}

