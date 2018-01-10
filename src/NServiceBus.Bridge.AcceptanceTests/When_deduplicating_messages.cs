using System;
using System.Collections.Generic;
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
public class When_deduplicating_messages : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_deliver_only_one_copy()
    {
        var deduplicationStore = new HashSet<string>();

        var bridgeConfig = Bridge.Between<MsmqTransport>("Left").And<MsmqTransport>("Right");
        bridgeConfig.InterceptForwarding((queue, message, dispatch, forward) =>
        {
            return forward(async (messages, transaction, context) =>
            {
                var duplicateMessages = messages.UnicastTransportOperations.Where(m => deduplicationStore.Contains(m.Message.Headers[Headers.MessageId]));
                foreach (var duplicate in duplicateMessages)
                {
                    messages.UnicastTransportOperations.Remove(duplicate);
                }
                await dispatch(messages, transaction, context).ConfigureAwait(false);
                foreach (var operation in messages.UnicastTransportOperations)
                {
                    deduplicationStore.Add(operation.Message.Headers[Headers.MessageId]);
                }
            });
        });
        bridgeConfig.LimitMessageProcessingConcurrencyTo(1); //To ensure ordering

        var result = await Scenario.Define<Context>()
            .With(bridgeConfig)
            .WithEndpoint<Sender>(c => c.When(async s =>
            {
                //Both messages have the same ID
                var messageId = Guid.NewGuid().ToString();

                var ops = new SendOptions();
                ops.SetMessageId(messageId);
                await s.Send(new MyMessage(), ops);

                ops = new SendOptions();
                ops.SetMessageId(messageId);
                await s.Send(new MyMessage(), ops);

                await s.Send(new TerminatorMessage());
            }))
            .WithEndpoint<Receiver>()
            .Done(c => c.TerminatorReceived)
            .Run();

        Assert.IsTrue(result.TerminatorReceived);
        Assert.AreEqual(1, result.MessageCopiesReceived);
    }

    class Context : ScenarioContext
    {
        public bool TerminatorReceived { get; set; }
        public int MessageCopiesReceived { get; set; }
    }

    class Sender : EndpointConfigurationBuilder
    {
        public Sender()
        {
            EndpointSetup<DefaultServer>(c =>
            {
                var routing = c.UseTransport<MsmqTransport>().Configure().Routing();
                var bridge = routing.ConnectToBridge("Left");
                bridge.RouteToEndpoint(typeof(MyMessage), Conventions.EndpointNamingConvention(typeof(Receiver)));
                bridge.RouteToEndpoint(typeof(TerminatorMessage), Conventions.EndpointNamingConvention(typeof(Receiver)));
            });
        }

       
    }

    class Receiver : EndpointConfigurationBuilder
    {
        public Receiver()
        {
            EndpointSetup<DefaultServer>(c =>
            {
                c.UseTransport<MsmqTransport>().Configure();
                c.LimitMessageProcessingConcurrencyTo(1); //To ensure ordering
            });
        }

        class MyMessageHandler : IHandleMessages<MyMessage>
        {
            Context scenarioContext;

            public MyMessageHandler(Context scenarioContext)
            {
                this.scenarioContext = scenarioContext;
            }

            public Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                scenarioContext.MessageCopiesReceived++;
                return Task.CompletedTask;
            }
        }

        class MerminatorMesageHandler : IHandleMessages<TerminatorMessage>
        {
            Context scenarioContext;

            public MerminatorMesageHandler(Context scenarioContext)
            {
                this.scenarioContext = scenarioContext;
            }

            public Task Handle(TerminatorMessage response, IMessageHandlerContext context)
            {
                scenarioContext.TerminatorReceived = true;
                return Task.CompletedTask;
            }
        }
    }

    class MyMessage : IMessage
    {
    }

    class TerminatorMessage : IMessage
    {
    }
}
