using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Pipeline;
using NServiceBus.Transport;

class SetCorrelationIdBehavior : Behavior<IOutgoingPhysicalMessageContext>
{
    static string ReplyIntent = MessageIntentEnum.Reply.ToString();

    public override Task Invoke(IOutgoingPhysicalMessageContext context, Func<Task> next)
    {
        if (string.Equals(context.Headers[Headers.MessageIntent], ReplyIntent, StringComparison.OrdinalIgnoreCase))
        {
            return next();
        }

        var replyToHeader = context.Headers[Headers.ReplyToAddress];
        var correlationId = context.Headers[Headers.CorrelationId];

        // pipe-separated TLV format
        var newCorrelationId = $"id|{correlationId.Length}|{correlationId}|reply-to|{replyToHeader.Length}|{replyToHeader}";
        context.Headers[Headers.CorrelationId] = newCorrelationId;

        return next();
    }
    
}