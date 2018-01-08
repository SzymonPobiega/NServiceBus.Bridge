using System;

namespace NServiceBus.Bridge
{
    /// <summary>
    /// An exception representing an error which causes a message to not be forwardable.
    /// </summary>
    public class UnforwardableMessageException : Exception
    {
        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public UnforwardableMessageException(string reason) : base(reason)
        {
        }
    }
}