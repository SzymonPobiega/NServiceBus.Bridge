using System;
using System.Threading.Tasks;
using NServiceBus.Pipeline;
using NServiceBus.Transport;

class ReplyBehavior : Behavior<IOutgoingReplyContext>
{
    public override Task Invoke(IOutgoingReplyContext context, Func<Task> next)
    {
        //IncomingMessage incomingMessage;
        //if (!context.TryGetIncomingPhysicalMessage(out incomingMessage))
        //{
        //    return next();
        //}

        //if (incomingMessage.Headers.TryGetValue("NServiceBus.Bridge.ReplyToAddress", out string replyTo))
        //{
        //    context.Headers["NServiceBus.Bridge.DestinationAddress"] = replyTo;
        //}
        return next();
    }
}