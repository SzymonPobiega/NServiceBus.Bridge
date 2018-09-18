#if NET461

using System;
using System.Threading.Tasks;
using Messages;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Bridge;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Features;
using NServiceBus.Serialization;
using NServiceBus.Settings;
using NUnit.Framework;
using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

[TestFixture]
public class When_publishing_from_asb_endpoint_oriented : NServiceBusAcceptanceTest
{
    static string PublisherEndpointName => Conventions.EndpointNamingConvention(typeof(Publisher));

    [Test]
    public async Task It_should_deliver_the_message_to_both_subscribers()
    {
        var resubscriberComponent = new ResubscriberComponent("Right");
        var bridgeConfiguration = PrepareBridgeConfiguration(resubscriberComponent);

        var result = await Scenario.Define<Context>()
            .With(bridgeConfiguration)
            .WithComponent(resubscriberComponent)
            .WithEndpoint<Publisher>(c => c.When(x => x.EventSubscribed, s => s.Publish(new MyAsbEvent())))
            .WithEndpoint<Subscriber>(c => c.When(async s =>
            {
                await s.Subscribe<MyAsbEvent>().ConfigureAwait(false);
                await s.Send(new TracerMessage()).ConfigureAwait(false);
            }))
            .Done(c => c.EventDelivered)
            .Run();

        Assert.IsTrue(result.EventDelivered);
        Console.WriteLine("Restarting");

        resubscriberComponent = new ResubscriberComponent("Right");
        bridgeConfiguration = PrepareBridgeConfiguration(resubscriberComponent);

        //No need to subscribe again. The subscription should have been created
        result = await Scenario.Define<Context>()
            .With(bridgeConfiguration)
            .WithComponent(resubscriberComponent)
            .WithEndpoint<Publisher>(c => c.When(x => x.EndpointsStarted, s => s.Publish(new MyAsbEvent())))
            .WithEndpoint<Subscriber>()
            .Done(c => c.EventDelivered)
            .Run();

        Assert.IsTrue(result.EventDelivered);
    }

    static BridgeConfiguration<AzureServiceBusTransport, TestTransport> PrepareBridgeConfiguration(ResubscriberComponent resubscriber)
    {
        var bridgeConfiguration = Bridge.Between<AzureServiceBusTransport>("Left", t =>
        {
            var connString = Environment.GetEnvironmentVariable("AzureServiceBus.ConnectionString");
            t.ConnectionString(connString);
            var settings = t.GetSettings();

            var builder = new ConventionsBuilder(settings);
            builder.DefiningEventsAs(x => x.Namespace == "Messages");
            settings.Set(builder.Conventions);

            var topology = t.UseEndpointOrientedTopology();
            topology.RegisterPublisher(typeof(MyAsbEvent), Conventions.EndpointNamingConvention(typeof(Publisher)));

            var serializer = Tuple.Create(new NewtonsoftSerializer() as SerializationDefinition, new SettingsHolder());
            settings.Set("MainSerializer", serializer);

            t.Transactions(TransportTransactionMode.SendsAtomicWithReceive);
        }).And<TestTransport>("Right", t =>
        {
            t.ConfigureNoNativePubSubBrokerA();
        });
        bridgeConfiguration.InterceptForwarding(resubscriber.InterceptMessageForwarding);
        bridgeConfiguration.LimitMessageProcessingConcurrencyTo(1); //To ensure when tracer arrives the subscribe request has already been processed.
        return bridgeConfiguration;
    }

    class Context : ScenarioContext
    {
        public bool EventDelivered { get; set; }
        public bool EventSubscribed { get; set; }
    }

    class Publisher : EndpointConfigurationBuilder
    {
        public Publisher()
        {
            EndpointSetup<DefaultServer>(c =>
            {
                //No bridge configuration needed for publisher
                var connString = Environment.GetEnvironmentVariable("AzureServiceBus.ConnectionString");
                var transport = c.UseTransport<AzureServiceBusTransport>();
                transport.ConnectionString(connString);
                transport.UseEndpointOrientedTopology();

                c.Conventions().DefiningEventsAs(x => x.Namespace == "Messages");
            });
        }

        class TracerHandler : IHandleMessages<TracerMessage>
        {
            Context scenarioContext;

            public TracerHandler(Context scenarioContext)
            {
                this.scenarioContext = scenarioContext;
            }

            public Task Handle(TracerMessage message, IMessageHandlerContext context)
            {
                scenarioContext.EventSubscribed = true;
                return Task.CompletedTask;
            }
        }
    }

    class Subscriber : EndpointConfigurationBuilder
    {
        public Subscriber()
        {
            EndpointSetup<DefaultServer>(c =>
            {
                c.DisableFeature<AutoSubscribe>();
                var routing = c.UseTransport<TestTransport>().ConfigureNoNativePubSubBrokerA().Routing();
                var ramp = routing.ConnectToBridge("Right");
                ramp.RegisterPublisher(typeof(MyAsbEvent), PublisherEndpointName);
                ramp.RouteToEndpoint(typeof(TracerMessage), PublisherEndpointName);

                c.Conventions().DefiningEventsAs(x => x.Namespace == "Messages");
            });
        }

        class BaseEventHandler : IHandleMessages<MyAsbEvent>
        {
            Context scenarioContext;

            public BaseEventHandler(Context scenarioContext)
            {
                this.scenarioContext = scenarioContext;
            }

            public Task Handle(MyAsbEvent message, IMessageHandlerContext context)
            {
                scenarioContext.EventDelivered = true;
                return Task.CompletedTask;
            }
        }
    }

    class TracerMessage : IMessage
    {
    }
}

namespace Messages
{
//Not nested because of sanitization rules
    class MyAsbEvent //: IEvent
    {
    }
}
#endif