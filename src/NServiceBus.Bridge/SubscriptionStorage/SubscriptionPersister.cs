using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus.Extensibility;
using NServiceBus.Unicast.Subscriptions;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

namespace NServiceBus.Bridge
{
    /// <summary>
    /// SQL-based subscription storage
    /// </summary>
    public class SqlSubscriptionStorage : ISubscriptionStorage
    {
        Func<DbConnection> connectionBuilder;
        TimeSpan? cacheFor;
        SubscriptionCommands subscriptionCommands;
        ConcurrentDictionary<string, CacheItem> cache;
        CommandBuilder commandBuilder;

        public SqlSubscriptionStorage(Func<DbConnection> connectionBuilder, string tablePrefix, SqlVariant sqlVariant, string schema, TimeSpan? cacheFor)
        {
            this.connectionBuilder = connectionBuilder;
            this.cacheFor = cacheFor;
            subscriptionCommands = SubscriptionCommandBuilder.Build(sqlVariant, tablePrefix, schema);
            commandBuilder = new CommandBuilder(sqlVariant);
            if (cacheFor != null)
            {
                cache = new ConcurrentDictionary<string, CacheItem>();
            }
        }

        public async Task Subscribe(Subscriber subscriber, MessageType messageType, ContextBag context)
        {
            using (var connection = await connectionBuilder.OpenConnection())
            using (var command = commandBuilder.CreateCommand(connection))
            {
                command.CommandText = subscriptionCommands.Subscribe;
                command.AddParameter("MessageType", messageType.TypeName);
                command.AddParameter("Subscriber", subscriber.TransportAddress);
                command.AddParameter("Endpoint", subscriber.Endpoint);
                await command.ExecuteNonQueryEx();
            }
            ClearForMessageType(messageType);
        }

        public async Task Unsubscribe(Subscriber subscriber, MessageType messageType, ContextBag context)
        {
            using (var connection = await connectionBuilder.OpenConnection())
            using (var command = commandBuilder.CreateCommand(connection))
            {
                command.CommandText = subscriptionCommands.Unsubscribe;
                command.AddParameter("MessageType", messageType.TypeName);
                command.AddParameter("Subscriber", subscriber.TransportAddress);
                await command.ExecuteNonQueryEx();
            }
            ClearForMessageType(messageType);
        }

        void ClearForMessageType(MessageType messageType)
        {
            if (cacheFor == null)
            {
                return;
            }
            var keyPart = GetKeyPart(messageType);
            foreach (var cacheKey in cache.Keys)
            {
                if (cacheKey.Contains(keyPart))
                {
                    CacheItem cacheItem;
                    cache.TryRemove(cacheKey, out cacheItem);
                }
            }
        }

        public Task<IEnumerable<Subscriber>> GetSubscriberAddressesForMessage(IEnumerable<MessageType> messageHierarchy, ContextBag context)
        {
            var types = messageHierarchy.ToList();

            if (cacheFor == null)
            {
                return GetSubscriptions(types);
            }

            var key = GetKey(types);

            var cacheItem = cache.GetOrAdd(key,
                valueFactory: _ => new CacheItem
                {
                    Stored = DateTime.UtcNow,
                    Subscribers = GetSubscriptions(types)
                });

            var age = DateTime.UtcNow - cacheItem.Stored;
            if (age >= cacheFor)
            {
                cacheItem.Subscribers = GetSubscriptions(types);
                cacheItem.Stored = DateTime.UtcNow;
            }
            return cacheItem.Subscribers;
        }

        static string GetKey(List<MessageType> types)
        {
            var typeNames = types.Select(_ => _.TypeName);
            return string.Join(",", typeNames) + ",";
        }

        static string GetKeyPart(MessageType type)
        {
            return $"{type.TypeName},";
        }

        internal class CacheItem
        {
            public DateTime Stored;
            public Task<IEnumerable<Subscriber>> Subscribers;
        }

        async Task<IEnumerable<Subscriber>> GetSubscriptions(List<MessageType> messageHierarchy)
        {
            var getSubscribersCommand = subscriptionCommands.GetSubscribers(messageHierarchy);
            using (var connection = await connectionBuilder.OpenConnection())
            using (var command = commandBuilder.CreateCommand(connection))
            {
                for (var i = 0; i < messageHierarchy.Count; i++)
                {
                    var messageType = messageHierarchy[i];
                    var paramName = $"type{i}";
                    command.AddParameter(paramName, messageType.TypeName);
                }
                command.CommandText = getSubscribersCommand;
                using (var reader = await command.ExecuteReaderAsync())
                {
                    var subscribers = new List<Subscriber>();
                    while (await reader.ReadAsync())
                    {
                        var address = reader.GetString(0);
                        string endpoint;
                        if (await reader.IsDBNullAsync(1))
                        {
                            endpoint = null;
                        }
                        else
                        {
                            endpoint = reader.GetString(1);
                        }
                        subscribers.Add(new Subscriber(address, endpoint));
                    }
                    return subscribers;
                }
            }
        }
    }
}