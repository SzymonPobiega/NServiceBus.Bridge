﻿#pragma warning disable 1591
namespace NServiceBus.Bridge
{
    using System;

    /// <summary>
    /// Not for public use.
    /// </summary>
    [Obsolete("Not for public use")]
    public static class SubscriptionCommandBuilder
    {
        public static SubscriptionCommands Build(SqlDialect sqlDialect, string tablePrefix)
        {
            var tableName = sqlDialect.GetSubscriptionTableName(tablePrefix);

            var subscribeCommand = sqlDialect.GetSubscriptionSubscribeCommand(tableName);
            var unsubscribeCommand = sqlDialect.GetSubscriptionUnsubscribeCommand(tableName);
            var getSubscribers = sqlDialect.GetSubscriptionQueryFactory(tableName);

            return new SubscriptionCommands(
                subscribe: subscribeCommand,
                unsubscribe: unsubscribeCommand,
                getSubscribers: getSubscribers);
        }
    }
}