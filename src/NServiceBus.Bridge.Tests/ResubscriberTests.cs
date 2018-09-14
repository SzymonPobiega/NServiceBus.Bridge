using System;
using System.Collections.Generic;
using System.Linq;
using System.Messaging;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Bridge;
using NServiceBus.Extensibility;
using NServiceBus.Raw;
using NServiceBus.Transport;
using NUnit.Framework;

[TestFixture]
public class ResubscriberTests
{
    [Test]
    public async Task It_deduplicates_entries_based_on_subscriber_and_type_by_discarding_older_messages()
    {
        var output = await RunResubscriberOnce(
            CreateMessageContext("type1", "sub1", MessageIntentEnum.Subscribe, "A", new DateTime(2018, 09, 14, 12, 56, 0)),
            CreateMessageContext("type1", "sub1", MessageIntentEnum.Subscribe, "B", new DateTime(2018, 09, 14, 12, 55, 0)));

        Assert.AreEqual(1, output.Count);
    }

    [Test]
    public async Task It_does_not_deduplicate_entries_messages_with_same_id()
    {
        var output = await RunResubscriberOnce(
            CreateMessageContext("type1", "sub1", MessageIntentEnum.Subscribe, "A", new DateTime(2018, 09, 14, 12, 56, 0)),
            CreateMessageContext("type1", "sub1", MessageIntentEnum.Subscribe, "A", new DateTime(2018, 09, 14, 12, 55, 0)));

        Assert.AreEqual(2, output.Count);
    }

    [Test]
    public async Task It_does_not_deduplicate_entries_messages_with_different_type()
    {
        var output = await RunResubscriberOnce(
            CreateMessageContext("type1", "sub1", MessageIntentEnum.Subscribe, "A", new DateTime(2018, 09, 14, 12, 56, 0)),
            CreateMessageContext("type2", "sub1", MessageIntentEnum.Subscribe, "B", new DateTime(2018, 09, 14, 12, 55, 0)));

        Assert.AreEqual(2, output.Count);
    }

    [Test]
    public async Task It_does_not_deduplicate_entries_messages_with_different_subscriber()
    {
        var output = await RunResubscriberOnce(
            CreateMessageContext("type1", "sub1", MessageIntentEnum.Subscribe, "A", new DateTime(2018, 09, 14, 12, 56, 0)),
            CreateMessageContext("type1", "sub2", MessageIntentEnum.Subscribe, "B", new DateTime(2018, 09, 14, 12, 55, 0)));

        Assert.AreEqual(2, output.Count);
    }

    [Test]
    public async Task It_does_not_discard_newer_messages()
    {
        var output = await RunResubscriberOnce(
            CreateMessageContext("type1", "sub1", MessageIntentEnum.Subscribe, "A", new DateTime(2018, 09, 14, 12, 56, 0)),
            CreateMessageContext("type1", "sub1", MessageIntentEnum.Subscribe, "B", new DateTime(2018, 09, 14, 12, 57, 0)));

        Assert.AreEqual(2, output.Count);
    }

    static async Task<List<MessageContext>> RunResubscriberOnce(params MessageContext[] input)
    {
        await Cleanup("ResubscriberTest", "ResubscriberTest.Resubscriber", "poison").ConfigureAwait(false);
        var done = new TaskCompletionSource<bool>();
        var output = new List<MessageContext>();
        var listenerConfig = RawEndpointConfiguration.Create("ResubscriberTest", (context, dispatcher) =>
        {
            if (context.Headers[Headers.SubscriptionMessageType] == "stop")
            {
                done.SetResult(true);
            }
            else
            {
                output.Add(context);
            }

            return Task.CompletedTask;
        }, "poison");
        listenerConfig.UseTransport<MsmqTransport>();
        listenerConfig.LimitMessageProcessingConcurrencyTo(1);
        listenerConfig.AutoCreateQueue();

        var listener = await RawEndpoint.Start(listenerConfig);

        var resubscriber = await Resubscriber<MsmqTransport>.Create("ResubscriberTest", TimeSpan.FromSeconds(3), t => { });

        foreach (var messageContext in input)
        {
            await resubscriber.InterceptMessageForwarding("ResubscriberTest", messageContext, () => Task.CompletedTask).ConfigureAwait(false);
        }

        await resubscriber.InterceptMessageForwarding("ResubscriberTest", CreateTerminatingContext(), () => Task.CompletedTask);

        await resubscriber.Start();

        await done.Task;

        await resubscriber.Stop();
        await listener.Stop();

        return output;
    }

    static MessageContext CreateTerminatingContext()
    {
        return CreateMessageContext("stop", "stop", MessageIntentEnum.Subscribe, Guid.NewGuid().ToString(), DateTime.UtcNow);
    }

    static MessageContext CreateMessageContext(string messageType, string replyTo, MessageIntentEnum intent, string id, DateTime timestamp)
    {
        const string ResubscriptionIdHeader = "NServiceBus.Bridge.ResubscriptionId";
        const string ResubscriptionTimestampHeader = "NServiceBus.Bridge.ResubscriptionTimestamp";

        var headers = new Dictionary<string, string>();
        if (messageType != null)
        {
            headers[Headers.SubscriptionMessageType] = messageType;
        }

        if (replyTo != null)
        {
            headers[Headers.ReplyToAddress] = replyTo;
        }

        headers[ResubscriptionIdHeader] = id;
        headers[ResubscriptionTimestampHeader] = timestamp.ToString("O");

        headers[Headers.MessageIntent] = intent.ToString();
        return new MessageContext(Guid.NewGuid().ToString(), headers, new byte[0], new TransportTransaction(), new CancellationTokenSource(), new ContextBag());
    }

    static Task Cleanup(params string[] queueNames)
    {
        var allQueues = MessageQueue.GetPrivateQueuesByMachine("localhost");
        var queuesToBeDeleted = new List<string>();

        foreach (var messageQueue in allQueues)
        {
            using (messageQueue)
            {
                if (queueNames.Any(ra =>
                {
                    var indexOfAt = ra.IndexOf("@", StringComparison.Ordinal);
                    if (indexOfAt >= 0)
                    {
                        ra = ra.Substring(0, indexOfAt);
                    }
                    return messageQueue.QueueName.StartsWith(@"private$\" + ra, StringComparison.OrdinalIgnoreCase);
                }))
                {
                    queuesToBeDeleted.Add(messageQueue.Path);
                }
            }
        }

        foreach (var queuePath in queuesToBeDeleted)
        {
            try
            {
                MessageQueue.Delete(queuePath);
                Console.WriteLine("Deleted '{0}' queue", queuePath);
            }
            catch (Exception)
            {
                Console.WriteLine("Could not delete queue '{0}'", queuePath);
            }
        }

        MessageQueue.ClearConnectionCache();

        return Task.FromResult(0);
    }

}
