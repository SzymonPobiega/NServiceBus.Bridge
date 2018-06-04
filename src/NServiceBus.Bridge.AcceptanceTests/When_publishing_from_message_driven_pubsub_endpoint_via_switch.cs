﻿using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Bridge;
using NUnit.Framework;
using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

[TestFixture]
public class When_publishing_from_message_driven_pubsub_endpoint_via_switch : NServiceBusAcceptanceTest
{
    [Test]
    public async Task It_should_deliver_the_message_to_both_subscribers()
    {
        var result = await Scenario.Define<Context>()
            .WithComponent(new SwitchComponent(() =>
            {
                var cfg = new SwitchConfiguration();
                cfg.AddPort<TestTransport>("Port1", t => { t.ConfigureNoNativePubSubBrokerA(); }).UseSubscriptionPersistence(new InMemorySubscriptionStorage());
                cfg.AddPort<TestTransport>("Port2", t => { t.ConfigureNoNativePubSubBrokerA(); }).UseSubscriptionPersistence(new InMemorySubscriptionStorage());
                cfg.AddPort<TestTransport>("Port3", t => { t.ConfigureNoNativePubSubBrokerA(); }).UseSubscriptionPersistence(new InMemorySubscriptionStorage());

                cfg.PortTable[Conventions.EndpointNamingConvention(typeof(Publisher))] = "Port1";
                return cfg;
            }))
            .WithEndpoint<Publisher>(c => c.When(x => x.BaseEventSubscribed && x.DerivedEventSubscribed, s => s.Publish(new MyDerivedEvent())))
            .WithEndpoint<BaseEventSubscriber>()
            .WithEndpoint<DerivedEventSubscriber>()
            .Done(c => c.BaseEventDelivered && c.DerivedEventDeilvered)
            .Run(TimeSpan.FromSeconds(20));

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
                c.UseTransport<TestTransport>().ConfigureNoNativePubSubBrokerA();

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
                var routing = c.UseTransport<TestTransport>().ConfigureNoNativePubSubBrokerA().Routing();
                var ramp = routing.ConnectToBridge("Port2");
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
                var routing = c.UseTransport<TestTransport>().ConfigureNoNativePubSubBrokerA().Routing();
                var ramp = routing.ConnectToBridge("Port3");
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
