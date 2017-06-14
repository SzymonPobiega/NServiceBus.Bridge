using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

static class Extensions
{
    public static async Task<DbConnection> OpenConnection(this Func<DbConnection> connectionBuilder)
    {
        var connection = connectionBuilder();
        await connection.OpenAsync();
        return connection;
    }

    public static Task ExecuteNonQueryEx(this DbCommand command)
    {
        return ExecuteNonQueryEx(command, CancellationToken.None);
    }

    public static async Task<int> ExecuteNonQueryEx(this DbCommand command, CancellationToken cancellationToken)
    {
        try
        {
            return await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            var message = $"Failed to ExecuteNonQuery. CommandText:{Environment.NewLine}{command.CommandText}";
            throw new Exception(message, exception);
        }
    }
    public static bool IsSubclassOfRawGeneric(this Type toCheck, Type generic)
    {
        while (toCheck != null && toCheck != typeof(object))
        {
            Type current;
            if (toCheck.IsGenericType)
            {
                current = toCheck.GetGenericTypeDefinition();
            }
            else
            {
                current = toCheck;
            }
            if (generic == current)
            {
                return true;
            }
            toCheck = toCheck.BaseType;
        }
        return false;
    }
}