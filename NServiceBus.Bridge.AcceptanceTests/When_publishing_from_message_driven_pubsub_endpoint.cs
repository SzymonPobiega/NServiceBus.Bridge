using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Bridge;
using NUnit.Framework;
using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

[TestFixture]
public class When_publishing_from_message_driven_pubsub_endpoint : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_deliver_the_reply_without_explicit_routing()
    {
        var result = await Scenario.Define<Context>()
            .With(Bridge.Between<MsmqTransport>("Left").And<MsmqTransport>("Right"))
            .WithEndpoint<Publisher>(c => c.When(x => x.BaseEventSubscribed && x.DerivedEventSubscribed, s => s.Publish(new MyDerivedEvent())))
            .WithEndpoint<BaseEventSubscriber>()
            .WithEndpoint<DerivedEventSubscriber>()
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
                //No publisher-side config required
                c.UseTransport<MsmqTransport>();

                c.OnEndpointSubscribed<Context>((args, context) =>
                {
                    if (args.MessageType.Contains("When_publishing_from_message_driven_pubsub_endpoint+MyBaseEvent"))
                    {
                        context.BaseEventSubscribed = true;
                    }
                    else
                    {
                        context.DerivedEventSubscribed = true;
                    }
                });
            });
        }
    }

    class BaseEventSubscriber : EndpointConfigurationBuilder
    {
        public BaseEventSubscriber()
        {
            EndpointSetup<DefaultServer>(c =>
            {
                var routing = c.UseTransport<MsmqTransport>().Routing();
                var ramp = routing.UseBridgeRamp("Right");
                ramp.RegisterPublisher(typeof(MyBaseEvent), Conventions.EndpointNamingConvention(typeof(Publisher)));
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
                var routing = c.UseTransport<MsmqTransport>().Routing();
                var ramp = routing.UseBridgeRamp("Right");
                ramp.RegisterPublisher(typeof(MyDerivedEvent), Conventions.EndpointNamingConvention(typeof(Publisher)));
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
}
