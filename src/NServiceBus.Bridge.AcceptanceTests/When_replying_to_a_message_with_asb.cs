#if NET461
using System;
using System.Threading.Tasks;
using System.Transactions;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Bridge;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Serialization;
using NServiceBus.Settings;
using NUnit.Framework;
using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

[TestFixture]
public class When_replying_to_a_message_with_asb : NServiceBusAcceptanceTest
{
    [Test]
    [Explicit]
    public async Task Should_deliver_the_reply_without_the_need_to_configure_the_bridge()
    {
        var bridgeConfig = Bridge.Between<TestTransport>("Left", t => t.Configure()).And<AzureServiceBusTransport>("Right", extensions =>
        {
            var connString = Environment.GetEnvironmentVariable("AzureServiceBus.ConnectionString");
            extensions.ConnectionString(connString);
            extensions.UseForwardingTopology();
            var settings = extensions.GetSettings();
            var serializer = Tuple.Create(new NewtonsoftSerializer() as SerializationDefinition, new SettingsHolder());
            settings.Set("MainSerializer", serializer);
        });
        bridgeConfig.CircuitBreakerThreshold = int.MaxValue;
        bridgeConfig.DelayedRetries = 0;
        bridgeConfig.InterceptForwarding((queue, message, dispatch, forward) =>
        {
            using (new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
            {
                return forward(dispatch);
            }
        });
        var result = await Scenario.Define<Context>()
            .With(bridgeConfig)
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
                var routing = c.UseTransport<TestTransport>().Routing();
                var ramp = routing.ConnectToBridge("Left");
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
                var connString = Environment.GetEnvironmentVariable("AzureServiceBus.ConnectionString");
                var transport = c.UseTransport<AzureServiceBusTransport>();
                transport.ConnectionString(connString);
                transport.UseForwardingTopology();
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
#endif