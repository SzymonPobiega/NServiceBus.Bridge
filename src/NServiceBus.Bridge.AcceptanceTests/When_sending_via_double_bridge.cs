using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Bridge;
using NUnit.Framework;
using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

[TestFixture]
public class When_sending_via_double_bridge : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_deliver_the_reply_without_the_need_to_configure_the_bridge()
    {
        var leftBridge = Bridge.Between<TestTransport>("LeftA", t => t.ConfigureNoNativePubSubBrokerA()).And<TestTransport>("LeftB", ext => ext.ConfigureNativePubSubBrokerB());
        leftBridge.Forwarding.ForwardTo(typeof(MyRequest).FullName, "RightB");

        var rightBridge = Bridge.Between<TestTransport>("RightA", t => t.ConfigureNoNativePubSubBrokerA()).And<TestTransport>("RightB", ext => ext.ConfigureNativePubSubBrokerB());

        var result = await Scenario.Define<Context>()
            .With(leftBridge)
            .With(rightBridge)
            .WithEndpoint<Sender>(c => c.When(s => s.Send(new MyRequest())))
            .WithEndpoint<Receiver>()
            .Done(c => c.RequestReceived && c.ResponseReceived)
            .Run();

        Assert.IsTrue(result.RequestReceived);
        Assert.IsTrue(result.ResponseReceived);
    }

    class Context : ScenarioContext
    {
        public bool RequestReceived { get; set; }
        public bool ResponseReceived { get; set; }
    }

    class Sender : EndpointConfigurationBuilder
    {
        public Sender()
        {
            EndpointSetup<DefaultServer>(c =>
            {
                var routing = c.UseTransport<TestTransport>().ConfigureNoNativePubSubBrokerA().Routing();
                var bridge = routing.ConnectToBridge("LeftA");
                bridge.RouteToEndpoint(typeof(MyRequest), Conventions.EndpointNamingConvention(typeof(Receiver)));
            });
        }

        class MyResponseHandler : IHandleMessages<MyResponse>
        {
            Context scenarioContext;

            public MyResponseHandler(Context scenarioContext)
            {
                this.scenarioContext = scenarioContext;
            }

            public Task Handle(MyResponse response, IMessageHandlerContext context)
            {
                scenarioContext.ResponseReceived = true;
                return Task.CompletedTask;
            }
        }
    }

    class Receiver : EndpointConfigurationBuilder
    {
        public Receiver()
        {
            EndpointSetup<DefaultServer>(c =>
            {
                //No bridge configuration needed for reply
                c.UseTransport<TestTransport>().ConfigureNoNativePubSubBrokerA();
            });
        }

        class MyRequestHandler : IHandleMessages<MyRequest>
        {
            Context scenarioContext;

            public MyRequestHandler(Context scenarioContext)
            {
                this.scenarioContext = scenarioContext;
            }

            public Task Handle(MyRequest request, IMessageHandlerContext context)
            {
                scenarioContext.RequestReceived = true;
                return context.Reply(new MyResponse());
            }
        }
    }

    class MyRequest : IMessage
    {
    }

    class MyResponse : IMessage
    {
    }
}
