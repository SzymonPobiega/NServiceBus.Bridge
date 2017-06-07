using System;

namespace NServiceBus.Bridge
{
    public class UnforwardableMessageException : Exception
    {
        public UnforwardableMessageException(string reason) : base(reason)
        {
        }
    }
}