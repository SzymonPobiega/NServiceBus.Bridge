using System;
using System.Data.Common;
using System.Threading.Tasks;

class CommandWrapper : IDisposable
{
    protected DbCommand command;

    public CommandWrapper(DbCommand command)
    {
        this.command = command;
    }

    public DbCommand InnerCommand => command;

    public string CommandText
    {
        get => command.CommandText;
        set => command.CommandText = value;
    }

    public DbTransaction Transaction
    {
        get => command.Transaction;
        set => command.Transaction = value;
    }

    public virtual void AddParameter(string name, object value)
    {
        var parameter = command.CreateParameter();
        ParameterFiller.Fill(parameter, name, value);
        command.Parameters.Add(parameter);
    }

    public void AddParameter(string name, Version value)
    {
        AddParameter(name, value.ToString());
    }

    public Task ExecuteNonQueryEx()
    {
        return command.ExecuteNonQueryEx();
    }
    
    public Task<DbDataReader> ExecuteReaderAsync()
    {
        return command.ExecuteReaderAsync();
    }

    public void Dispose()
    {
        command.Dispose();
    }
}