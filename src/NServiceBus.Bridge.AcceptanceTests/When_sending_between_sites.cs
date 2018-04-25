using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Bridge;
using NUnit.Framework;
using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

[TestFixture]
public class When_sending_between_sites : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_deliver_the_reply_back()
    {
        var leftBridge = Bridge
            .Between<TestTransport>("BridgeA", t => t.ConfigureNoNativePubSubBrokerA())
            .And<TestTransport>("BridgeA.Channel", ext => ext.ConfigureNativePubSubBrokerC());

        leftBridge.ConfigureSites().AddSite("B", "BridgeB.Channel");

        var rightBridge = Bridge
            .Between<TestTransport>("BridgeB", t => t.ConfigureNativePubSubBrokerB())
            .And<TestTransport>("BridgeB.Channel", ext => ext.ConfigureNativePubSubBrokerC());

        //This is not required to route the reply
        //rightBridge.ConfigureSites().AddSite("A", "BridgeA.Channel");

        var result = await Scenario.Define<Context>()
            .With(leftBridge)
            .With(rightBridge)
            .WithEndpoint<Sender>(c => c.When(s =>
            {
                var ops = new SendOptions();
                ops.SendToSites("B");
                return s.Send(new MyRequest(), ops);
            }))
            .WithEndpoint<Receiver>()
            .Done(c => c.RequestReceived && c.ResponseReceived)
            .Run(TimeSpan.FromSeconds(30));

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
                var bridge = routing.ConnectToBridge("BridgeA");
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
                c.UseTransport<TestTransport>().ConfigureNativePubSubBrokerB();
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
