using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Bridge;
using NServiceBus.Persistence;
using NUnit.Framework;
using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

[TestFixture]
public class When_using_NHibernate_persistence : NServiceBusAcceptanceTest
{
    [Test]
    public async Task It_should_deliver_the_message_to_both_subscribers()
    {
        var result = await Scenario.Define<Context>()
            .With(() =>
            {
                var config = Bridge.Between<MsmqTransport>("Left").And<MsmqTransport>("Right");
                config.UseSubscriptionPersistece<NHibernatePersistence>((e, c) =>
                {
                    c.ConnectionString(@"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True");
                    e.EnableInstallers();
                });
                return config;
            })
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
                //No bridge configuration needed for publisher
                c.UseTransport<MsmqTransport>();

                c.OnEndpointSubscribed<Context>((args, context) =>
                {
                    if (args.MessageType.Contains("MyBaseEvent"))
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
                var ramp = routing.ConnectToBridge("Right");
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
                var ramp = routing.ConnectToBridge("Right");
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
