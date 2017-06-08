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
    public async Task Should_deliver_the_reply_without_explicit_routing()
    {
        var bridgeConfiguration = Bridge.Between<RabbitMQTransport>("Left", t => t.ConnectionString("host=localhost")).And<MsmqTransport>("Right");
        bridgeConfiguration.LimitMessageProcessingConcurrencyTo(1); //To ensure when tracer arrives the subscribe request has already been processed.

        var result = await Scenario.Define<Context>()
            .With(bridgeConfiguration)
            .WithEndpoint<Publisher>(c => c.When(x => x.BaseEventSubscribed && x.DerivedEventSubscribed, s => s.Publish(new MyDerivedEvent())))
            .WithEndpoint<BaseEventSubscriber>(c => c.When(async s =>
            {
                await s.Subscribe<MyBaseEvent>().ConfigureAwait(false);
                await s.Send(new TracerMessage()).ConfigureAwait(false);
            }))
            .WithEndpoint<DerivedEventSubscriber>(c => c.When(async s =>
            {
                await s.Subscribe<MyDerivedEvent>().ConfigureAwait(false);
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
                //No publisher-side config required
                c.UseTransport<RabbitMQTransport>().ConnectionString("host=localhost");
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
                var routing = c.UseTransport<MsmqTransport>().Routing();
                var ramp = routing.UseBridgeRamp("Right");
                ramp.RegisterPublisher(typeof(MyBaseEvent), PublisherEndpointName);
                ramp.RouteToEndpoint(typeof(TracerMessage), PublisherEndpointName);
            });
        }

        class BaseEventHandler : IHandleMessages<MyBaseEvent>
        {
            Context scenarioContext;

            public BaseEventHandler(Context scenarioContext)
            {
                this.scenarioContext = scenarioContext;
            }

            public Task Handle(MyBaseEvent message, IMessageHandlerContext context)
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
                var routing = c.UseTransport<MsmqTransport>().Routing();
                var ramp = routing.UseBridgeRamp("Right");
                ramp.RegisterPublisher(typeof(MyDerivedEvent), PublisherEndpointName);
                ramp.RouteToEndpoint(typeof(TracerMessage), PublisherEndpointName);
            });
        }

        class DerivedEventHandler : IHandleMessages<MyDerivedEvent>
        {
            Context scenarioContext;

            public DerivedEventHandler(Context scenarioContext)
            {
                this.scenarioContext = scenarioContext;
            }

            public Task Handle(MyDerivedEvent message, IMessageHandlerContext context)
            {
                scenarioContext.DerivedEventDeilvered = true;
                return Task.CompletedTask;
            }
        }
    }

    class MyBaseEvent : IEvent
    {
    }

    class MyDerivedEvent : MyBaseEvent
    {
    }

    class TracerMessage : IMessage
    {
    }
}
