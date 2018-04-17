using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Bridge;
using NUnit.Framework;
using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

[TestFixture]
public class When_publishing_via_double_unicast_bridge : NServiceBusAcceptanceTest
{
    [Test]
    public async Task It_should_deliver_the_message()
    {
        var leftBridge = Bridge.Between<TestTransport>("Left", t => t.ConfigureNoNativePubSubBrokerA()).And<TestTransport>("MiddleLeft", t => t.ConfigureNoNativePubSubBrokerA());
        var rightBridge = Bridge.Between<TestTransport>("Right", t => t.ConfigureNoNativePubSubBrokerA()).And<TestTransport>("MiddleRight", t => t.ConfigureNoNativePubSubBrokerA());
        rightBridge.InterceptForwarding((queue, message, dispatch, forward) =>
        {
            return forward((messages, transaction, context) =>
            {
                if (messages.UnicastTransportOperations.Any(m => m.Destination.Contains("Publisher")))
                {
                    Assert.Fail("Right bridge cannot forward directly to publisher.");
                }
                return dispatch(messages, transaction, context);
            });
        });
        rightBridge.Forwarding.RegisterPublisher(typeof(MyEvent).FullName, "MiddleLeft");
        
        var result = await Scenario.Define<Context>()
            .With(leftBridge)
            .With(rightBridge)
            .WithEndpoint<Publisher>(c => c.When(x => x.EventSubscribed, s => s.Publish(new MyEvent())))
            .WithEndpoint<Subscriber>()
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
                c.UseTransport<TestTransport>().ConfigureNoNativePubSubBrokerA();

                c.OnEndpointSubscribed<Context>((args, context) =>
                {
                    context.EventSubscribed = true;
                });
            });
        }
    }

    class Subscriber : EndpointConfigurationBuilder
    {
        public Subscriber()
        {
            EndpointSetup<DefaultServer>(c =>
            {
                var routing = c.UseTransport<TestTransport>().ConfigureNoNativePubSubBrokerA().Routing();
                var ramp = routing.ConnectToBridge("Right");
                ramp.RegisterPublisher(typeof(MyEvent), Conventions.EndpointNamingConvention(typeof(Publisher)));
            });
        }

        class MyEventHandler : IHandleMessages<MyEvent>
        {
            Context scenarioContext;

            public MyEventHandler(Context scenarioContext)
            {
                this.scenarioContext = scenarioContext;
            }

            public Task Handle(MyEvent message, IMessageHandlerContext context)
            {
                scenarioContext.EventDelivered = true;
                return Task.CompletedTask;
            }
        }
    }

    class MyEvent : IEvent
    {
    }
}
