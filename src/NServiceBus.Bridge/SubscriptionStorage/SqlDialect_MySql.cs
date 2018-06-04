﻿namespace NServiceBus.Bridge
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Text;
    using Unicast.Subscriptions;

    public partial class SqlDialect
    {
        /// <summary>
        /// MySQL dialect
        /// </summary>
        public class MySql : SqlDialect
        {
            internal override void SetParameterValue(DbParameter parameter, object value)
            {
                parameter.Value = value;
            }

            internal override CommandWrapper CreateCommand(DbConnection connection)
            {
                var command = connection.CreateCommand();
                return new CommandWrapper(command, this);
            }

            internal override string GetSubscriptionTableName(string tablePrefix)
            {
                return $"`{tablePrefix}SubscriptionData`";
            }

            internal override string GetSubscriptionSubscribeCommand(string tableName)
            {
                return $@"
insert into {tableName}
(
    Subscriber,
    MessageType,
    Endpoint,
    PersistenceVersion
)
values
(
    @Subscriber,
    @MessageType,
    @Endpoint,
    @PersistenceVersion
)
on duplicate key update
    Endpoint = @Endpoint,
    PersistenceVersion = @PersistenceVersion
";
            }

            internal override string GetSubscriptionUnsubscribeCommand(string tableName)
            {
                return $@"
delete from {tableName}
where
    Subscriber = @Subscriber and
    MessageType = @MessageType";
            }

            internal override Func<List<MessageType>, string> GetSubscriptionQueryFactory(string tableName)
            {
                var getSubscribersPrefix = $@"
select distinct Subscriber, Endpoint
from {tableName}
where MessageType in (";

                return messageTypes =>
                {
                    var builder = new StringBuilder(getSubscribersPrefix);
                    for (var i = 0; i < messageTypes.Count; i++)
                    {
                        var paramName = $"@type{i}";
                        builder.Append(paramName);
                        if (i < messageTypes.Count - 1)
                        {
                            builder.Append(", ");
                        }
                    }
                    builder.Append(")");
                    return builder.ToString();
                };
            }
        }
    }
}