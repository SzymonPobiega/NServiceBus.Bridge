using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Bridge;
using NUnit.Framework;
using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

[TestFixture]
public class When_replying_to_a_message : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_deliver_the_reply_without_explicit_routing()
    {
        var result = await Scenario.Define<Context>()
            .WithComponent(new BridgeComponent<MsmqTransport, MsmqTransport>(Bridge.Between<MsmqTransport>("Left").And<MsmqTransport>("Right")))
            .WithEndpoint<Sender>(c => c.When(s => s.Send(new MyRequest())))
            .WithEndpoint<Receiver>()
            .Done(c => c.RequestReceived && c.ResponseReceived)
            .Run();

        Assert.IsTrue(result.RequestReceived);
        Assert.IsTrue(result.ResponseReceived);
        Assert.IsTrue(result.RequestHasBridgeReplyToHeader);
        Assert.IsFalse(result.RequestHasBridgeDestinationHeader);
        Assert.IsFalse(result.ResponseHasBridgeDestinationHeader);
    }

    class Context : ScenarioContext
    {
        public bool RequestReceived { get; set; }
        public bool ResponseReceived { get; set; }
        public bool RequestHasBridgeReplyToHeader { get; set; }
        public bool ResponseHasBridgeDestinationHeader { get; set; }
        public bool RequestHasBridgeDestinationHeader { get; set; }
    }

    class Sender : EndpointConfigurationBuilder
    {
        public Sender()
        {
            EndpointSetup<DefaultServer>(c =>
            {
                var routing = c.UseTransport<MsmqTransport>().Routing();
                var ramp = routing.UseBridgeRamp("Left");
                ramp.RouteToEndpoint(typeof(MyRequest), Conventions.EndpointNamingConvention(typeof(Receiver)));
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
                scenarioContext.ResponseHasBridgeDestinationHeader = context.MessageHeaders.ContainsKey("NServiceBus.Bridge.DestinationAddress");
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
                var routing = c.UseTransport<MsmqTransport>().Routing();
                routing.UseBridgeRamp("Right");
                //No explicit routing needed
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
                scenarioContext.RequestHasBridgeReplyToHeader = context.MessageHeaders.ContainsKey("NServiceBus.Bridge.ReplyToAddress");
                scenarioContext.RequestHasBridgeDestinationHeader = context.MessageHeaders.ContainsKey("NServiceBus.Bridge.DestinationEndpoint");
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
