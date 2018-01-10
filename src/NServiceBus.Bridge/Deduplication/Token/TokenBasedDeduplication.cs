using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NServiceBus.Bridge.Deduplication.Token
{
    using Transport;

    class TokenBasedDeduplication
    {
        IOutboxPersistence persistence;
        ITokenStore tokenStore;

        public async Task Deduplicate(string inputQueue, MessageContext message, Dispatch dispatchLocal, Dispatch dispatchForward, Func<Dispatch, Task> forwardMethod)
        {
            if (inputQueue == "Left")
            {
                //Storing
                var logicalMessageId = message.Headers[Headers.MessageId];
                var outgoingOps = new List<IOutgoingTransportOperation>();

                await persistence.CreateRecord(logicalMessageId);

                await forwardMethod((messages, transaction, context) =>
                {
                    //Record outgoing operations in memory.
                    outgoingOps.AddRange(messages.UnicastTransportOperations);
                    outgoingOps.AddRange(messages.MulticastTransportOperations);
                    return Task.CompletedTask;
                }).ConfigureAwait(false);

                //Store outgoing operations
                await persistence.Store(logicalMessageId, outgoingOps, message.TransportTransaction); //Updates the record with operations

                //Send a message to push outgoing messages
                await dispatchLocal(new TransportOperations());

                
                //Dispatching
                var outgoingMessages = persistence.Load(logicalMessageId);
                if (outgoingMessages == null)
                {
                    return; //Already dispatched
                }
                //Create token
                //Mark token created
                //Dispatch
                //Remove
            }
            else
            {
                
                //Receiving
                //Load token
                //Claim token
                //Create inbox record
                //Push inbox record
            }
        }
    }

    interface IOutboxPersistence
    {
        
    }

    interface ITokenStore
    {
        
    }
}
