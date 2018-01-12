using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Bridge;
using NUnit.Framework;
using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

[TestFixture]
public class When_subscribing_from_native_and_message_driven_endpoints : NServiceBusAcceptanceTest
{
    static string PublisherEndpointName => Conventions.EndpointNamingConvention(typeof(Publisher));

    [Test]
    public async Task It_should_deliver_the_message_to_both_subscribers()
    {
        var result = await Scenario.Define<Context>()
            .WithComponent(new SwitchComponent(() =>
            {
                var cfg = new SwitchConfiguration();

                //Publisher - RabbitMQ. Limit concurrency to ensure when tracer arrives the subscribe request has already been processed.
                cfg.AddPort<RabbitMQTransport>("Port1", t => t.Configure()).LimitMessageProcessingConcurrencyTo(1);

                //BaseEventSubscriber - MSMQ
                cfg.AddPort<MsmqTransport>("Port2", t => { }).UseSubscriptionPersistence<InMemoryPersistence>(c => { });

                //DerivedEventSubscriber - RabbitMQ
                cfg.AddPort<RabbitMQTransport>("Port3", t => t.Configure());

                cfg.PortTable[Conventions.EndpointNamingConvention(typeof(Publisher))] = "Port1";
                return cfg;
            }))
            .WithEndpoint<Publisher>(c => c.When(x => x.BaseEventSubscribed && x.DerivedEventSubscribed, s => s.Publish(new MyDerivedEvent3())))
            .WithEndpoint<BaseEventSubscriber>(c => c.When(async s =>
            {
                await s.Subscribe<MyBaseEvent3>().ConfigureAwait(false);
                await s.Send(new TracerMessage()).ConfigureAwait(false);
            }))
            .WithEndpoint<DerivedEventSubscriber>(c => c.When(async s =>
            {
                await s.Subscribe<MyDerivedEvent3>().ConfigureAwait(false);
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
        public bool DerivedEventSubscribed { get; set; }
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
                var routing = c.UseTransport<MsmqTransport>().Configure().Routing();
                var bridge = routing.ConnectToBridge("Port2");
                bridge.RegisterPublisher(typeof(MyBaseEvent3), Conventions.EndpointNamingConvention(typeof(Publisher)));
                bridge.RouteToEndpoint(typeof(TracerMessage), PublisherEndpointName);
            });
        }

        class BaseEventHandler : IHandleMessages<MyBaseEvent3>
        {
            Context scenarioContext;

            public BaseEventHandler(Context scenarioContext)
            {
                this.scenarioContext = scenarioContext;
            }

            public Task Handle(MyBaseEvent3 message, IMessageHandlerContext context)
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
                var routing = c.UseTransport<RabbitMQTransport>().Configure()
                    .Routing();

                var bridge = routing.ConnectToBridge("Port3");
                bridge.RegisterPublisher(typeof(MyDerivedEvent3), Conventions.EndpointNamingConvention(typeof(Publisher)));
                bridge.RouteToEndpoint(typeof(TracerMessage), PublisherEndpointName);
            });
        }

        class DerivedEventHandler : IHandleMessages<MyDerivedEvent3>
        {
            Context scenarioContext;

            public DerivedEventHandler(Context scenarioContext)
            {
                this.scenarioContext = scenarioContext;
            }

            public Task Handle(MyDerivedEvent3 message, IMessageHandlerContext context)
            {
                scenarioContext.DerivedEventDeilvered = true;
                return Task.CompletedTask;
            }
        }
    }

    class MyBaseEvent3 : IEvent
    {
    }

    class MyDerivedEvent3 : MyBaseEvent3
    {
    }

    class TracerMessage : IMessage
    {
    }
}
