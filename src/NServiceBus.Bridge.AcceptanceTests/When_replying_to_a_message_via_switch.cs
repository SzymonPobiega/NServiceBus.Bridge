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
public class When_replying_to_a_message_via_switch : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_deliver_the_reply_without_the_need_to_configure_the_hub()
    {
        var result = await Scenario.Define<Context>()
            .WithComponent(new SwitchComponent(() =>
            {
                var cfg = new SwitchConfiguration();
                cfg.AddPort<MsmqTransport>("Port1", t => { }).UseSubscriptionPersistence(new InMemorySubscriptionStorage());
                cfg.AddPort<MsmqTransport>("Port2", t => { }).UseSubscriptionPersistence(new InMemorySubscriptionStorage());
                cfg.AddPort<MsmqTransport>("Port3", t => { }).UseSubscriptionPersistence(new InMemorySubscriptionStorage());

                cfg.PortTable[Conventions.EndpointNamingConvention(typeof(Sender1))] = "Port1";
                cfg.PortTable[Conventions.EndpointNamingConvention(typeof(Sender2))] = "Port2";
                //Sender3's port is configured manually in Sender2

                return cfg;
            }))
            .WithEndpoint<Sender1>(c => c.When(s => s.Send(new MyRequest())))
            .WithEndpoint<Sender2>(c => c.When(s => s.Send(new MyRequest())))
            .WithEndpoint<Sender3>(c => c.When(s => s.Send(new MyRequest())))
            .Done(c => c.Response1Received && c.Response2Received && c.Response3Received)
            .Run(TimeSpan.FromSeconds(20));

        Assert.IsTrue(result.Request1Received);
        Assert.IsTrue(result.Response1Received);
        Assert.IsTrue(result.Request2Received);
        Assert.IsTrue(result.Response2Received);
        Assert.IsTrue(result.Request3Received);
        Assert.IsTrue(result.Response3Received);
    }

    class Context : ScenarioContext
    {
        public bool Request1Received { get; set; }
        public bool Response1Received { get; set; }

        public bool Request2Received { get; set; }
        public bool Response2Received { get; set; }

        public bool Request3Received { get; set; }
        public bool Response3Received { get; set; }
    }

    class Sender1 : EndpointConfigurationBuilder
    {
        public Sender1()
        {
            EndpointSetup<DefaultServer>(c =>
            {
                var routing = c.UseTransport<MsmqTransport>().Configure().Routing();
                var bridge = routing.ConnectToBridge("Port1");
                bridge.RouteToEndpoint(typeof(MyRequest), Conventions.EndpointNamingConvention(typeof(Sender2)));
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
                scenarioContext.Response1Received = true;
                return Task.CompletedTask;
            }
        }

        class MyRequestHandler : IHandleMessages<MyRequest>
        {
            Context scenarioContext;

            public MyRequestHandler(Context scenarioContext)
            {
                this.scenarioContext = scenarioContext;
            }

            public Task Handle(MyRequest response, IMessageHandlerContext context)
            {
                scenarioContext.Request3Received = true;
                return context.Reply(new MyResponse());
            }
        }
    }

    class Sender2 : EndpointConfigurationBuilder
    {
        public Sender2()
        {
            EndpointSetup<DefaultServer>(c =>
            {
                var sender3 = Conventions.EndpointNamingConvention(typeof(Sender3));

                var routing = c.UseTransport<MsmqTransport>().Configure().Routing();
                var bridge = routing.ConnectToBridge("Port2");
                bridge.RouteToEndpoint(typeof(MyRequest), sender3);
                bridge.SetPort(sender3, "Port3");
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
                scenarioContext.Response2Received = true;
                return Task.CompletedTask;
            }
        }

        class MyRequestHandler : IHandleMessages<MyRequest>
        {
            Context scenarioContext;

            public MyRequestHandler(Context scenarioContext)
            {
                this.scenarioContext = scenarioContext;
            }

            public Task Handle(MyRequest response, IMessageHandlerContext context)
            {
                scenarioContext.Request1Received = true;
                return context.Reply(new MyResponse());
            }
        }
    }

    class Sender3 : EndpointConfigurationBuilder
    {
        public Sender3()
        {
            EndpointSetup<DefaultServer>(c =>
            {
                var routing = c.UseTransport<MsmqTransport>().Configure().Routing();
                var bridge = routing.ConnectToBridge("Port3");
                bridge.RouteToEndpoint(typeof(MyRequest), Conventions.EndpointNamingConvention(typeof(Sender1)));
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
                scenarioContext.Response3Received = true;
                return Task.CompletedTask;
            }
        }

        class MyRequestHandler : IHandleMessages<MyRequest>
        {
            Context scenarioContext;

            public MyRequestHandler(Context scenarioContext)
            {
                this.scenarioContext = scenarioContext;
            }

            public Task Handle(MyRequest response, IMessageHandlerContext context)
            {
                scenarioContext.Request2Received = true;
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
