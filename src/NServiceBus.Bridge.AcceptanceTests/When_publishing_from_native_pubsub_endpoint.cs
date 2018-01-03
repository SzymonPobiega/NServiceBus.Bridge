using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Bridge;
using NServiceBus.Features;
using NUnit.Framework;
using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

[TestFixture]
public class When_publishing_from_native_pubsub_endpoint : NServiceBusAcceptanceTest
{
    static string PublisherEndpointName => Conventions.EndpointNamingConvention(typeof(Publisher));

    [Test]
    public async Task It_should_deliver_the_message_to_both_subscribers()
    {
        var bridgeConfiguration = Bridge.Between<RabbitMQTransport>("Left", t => t.Configure()).And<MsmqTransport>("Right");
        bridgeConfiguration.LimitMessageProcessingConcurrencyTo(1); //To ensure when tracer arrives the subscribe request has already been processed.

        var result = await Scenario.Define<Context>()
            .With(bridgeConfiguration)
            .WithEndpoint<Publisher>(c => c.When(x => x.BaseEventSubscribed && x.DerivedEventSubscribed, s => s.Publish(new MyDerivedEvent1())))
            .WithEndpoint<BaseEventSubscriber>(c => c.When(async s =>
            {
                await s.Subscribe<MyBaseEvent1>().ConfigureAwait(false);
                await s.Send(new TracerMessage()).ConfigureAwait(false);
            }))
            .WithEndpoint<DerivedEventSubscriber>(c => c.When(async s =>
            {
                await s.Subscribe<MyDerivedEvent1>().ConfigureAwait(false);
                await s.Send(new TracerMessage()).ConfigureAwait(false);
            }))
            .Done(c => c.BaseEventDelivered && c.DerivedEventDeilvered)
            .Run();

        Assert.IsTrue(result.BaseEventDelivered);
        Assert.IsTrue(result.DerivedEventDeilvered);
    }

    class Context : ScenarioContext
    {
        public bool BaseEventDelivered { get; set; }
        public bool DerivedEventDeilvered { get; set; }
        public bool BaseEventSubscribed { get; set; }
        public bool DerivedEventSubscribed { get; set; } = true;
    }

    class Publisher : EndpointConfigurationBuilder
    {
        public Publisher()
        {
            EndpointSetup<DefaultServer>(c =>
            {
                //No bridge configuration needed for publisher
                c.UseTransport<RabbitMQTransport>().Configure();
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
                if (context.MessageHeaders[Headers.OriginatingEndpoint].Contains("BaseEventSubscriber"))
                {
                    scenarioContext.BaseEventSubscribed = true;
                }
                else
                {
                    scenarioContext.DerivedEventSubscribed = true;
                }
                return Task.CompletedTask;
            }
        }
    }

    class BaseEventSubscriber : EndpointConfigurationBuilder
    {
        public BaseEventSubscriber()
        {
            EndpointSetup<DefaultServer>(c =>
            {
                c.DisableFeature<AutoSubscribe>();
                var routing = c.UseTransport<MsmqTransport>().Configure().Routing();
                var bridge = routing.ConnectToBridge("Right");
                bridge.RegisterPublisher(typeof(MyBaseEvent1), PublisherEndpointName);
                bridge.RouteToEndpoint(typeof(TracerMessage), PublisherEndpointName);
            });
        }

        class BaseEventHandler : IHandleMessages<MyBaseEvent1>
        {
            Context scenarioContext;

            public BaseEventHandler(Context scenarioContext)
            {
                this.scenarioContext = scenarioContext;
            }

            public Task Handle(MyBaseEvent1 message, IMessageHandlerContext context)
            {
                scenarioContext.BaseEventDelivered = true;
                return Task.CompletedTask;
            }
        }
    }

    class DerivedEventSubscriber : EndpointConfigurationBuilder
    {
        public DerivedEventSubscriber()
        {
            EndpointSetup<DefaultServer>(c =>
            {
                c.DisableFeature<AutoSubscribe>();
                var routing = c.UseTransport<MsmqTransport>().Configure().Routing();
                var bridge = routing.ConnectToBridge("Right");
                bridge.RegisterPublisher(typeof(MyDerivedEvent1), PublisherEndpointName);
                bridge.RouteToEndpoint(typeof(TracerMessage), PublisherEndpointName);
            });
        }

        class DerivedEventHandler : IHandleMessages<MyDerivedEvent1>
        {
            Context scenarioContext;

            public DerivedEventHandler(Context scenarioContext)
            {
                this.scenarioContext = scenarioContext;
            }

            public Task Handle(MyDerivedEvent1 message, IMessageHandlerContext context)
            {
                scenarioContext.DerivedEventDeilvered = true;
                return Task.CompletedTask;
            }
        }
    }

    class MyBaseEvent1 : IEvent
    {
    }

    class MyDerivedEvent1 : MyBaseEvent1
    {
    }

    class TracerMessage : IMessage
    {
    }
}
