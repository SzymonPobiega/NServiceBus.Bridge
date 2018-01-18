using System;
using System.Threading.Tasks;
using System.Transactions;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Bridge;
using NServiceBus.Features;
using NUnit.Framework;
using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

[TestFixture]
public class When_publishing_from_asb_endpoint_oriented : NServiceBusAcceptanceTest
{
    static string PublisherEndpointName => Conventions.EndpointNamingConvention(typeof(Publisher));

    [Test]
    public async Task It_should_deliver_the_message_to_both_subscribers()
    {
        var bridgeConfiguration = Bridge.Between<AzureServiceBusTransport>("Left", t =>
        {
            var connString = Environment.GetEnvironmentVariable("AzureServiceBus.ConnectionString");
            t.ConnectionString(connString);
            var topology = t.UseEndpointOrientedTopology();
            topology.RegisterPublisher(typeof(MyAsbEvent), Conventions.EndpointNamingConvention(typeof(Publisher)));

        }).And<MsmqTransport>("Right");

        bridgeConfiguration.TypeGenerator.RegisterKnownType(typeof(MyAsbEvent));
        bridgeConfiguration.InterceptForawrding(async (queue, message, dispatch) =>
        {
            using (new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
            {
                await dispatch();
            }
        });

        bridgeConfiguration.LimitMessageProcessingConcurrencyTo(1); //To ensure when tracer arrives the subscribe request has already been processed.

        var result = await Scenario.Define<Context>()
            .With(bridgeConfiguration)
            .WithEndpoint<Publisher>(c => c.When(x => x.EventSubscribed, s => s.Publish(new MyAsbEvent())))
            .WithEndpoint<Subscriber>(c => c.When(async s =>
            {
                await s.Subscribe<MyAsbEvent>().ConfigureAwait(false);
                await s.Send(new TracerMessage()).ConfigureAwait(false);
            }))
            .Done(c => c.EventDelivered)
            .Run();

        Assert.IsTrue(result.EventDelivered);
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
                var routing = c.UseTransport<MsmqTransport>().Routing();
                var ramp = routing.ConnectToBridge("Right");
                ramp.RegisterPublisher(typeof(MyAsbEvent), PublisherEndpointName);
                ramp.RouteToEndpoint(typeof(TracerMessage), PublisherEndpointName);
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

//Not nested because of sanitization rules
class MyAsbEvent : IEvent
{
}
