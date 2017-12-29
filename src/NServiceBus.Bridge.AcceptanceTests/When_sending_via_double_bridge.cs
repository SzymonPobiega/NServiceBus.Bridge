﻿using System.Threading.Tasks;
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
        var leftBridge = Bridge.Between<MsmqTransport>("LeftMSMQ").And<RabbitMQTransport>("LeftRabbit", ext =>
        {
            ext.ConnectionString("host=localhost");
            ext.UseConventionalRoutingTopology();
        });
        leftBridge.Forwarding.ForwardTo(typeof(MyRequest).FullName, "RightRabbit");

        var rightBridge = Bridge.Between<MsmqTransport>("RightMSMQ").And<RabbitMQTransport>("RightRabbit", ext =>
        {
            ext.ConnectionString("host=localhost");
            ext.UseConventionalRoutingTopology();
        });

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
                var routing = c.UseTransport<MsmqTransport>().Routing();
                var ramp = routing.ConnectToBridge("LeftMSMQ");
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
                c.UseTransport<MsmqTransport>();
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
