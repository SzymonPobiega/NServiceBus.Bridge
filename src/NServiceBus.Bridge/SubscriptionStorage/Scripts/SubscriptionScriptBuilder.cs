using System.IO;
using System.Text;
using NServiceBus.Bridge;

static class SubscriptionScriptBuilder
{
    public static void BuildCreateScript(TextWriter writer, SqlVariant sqlVariant)
    {
        writer.Write(ReadResource(sqlVariant, "Create"));
    }

    public static string BuildCreateScript(SqlVariant sqlVariant)
    {
        var stringBuilder = new StringBuilder();
        using (var stringWriter = new StringWriter(stringBuilder))
        {
            BuildCreateScript(stringWriter, sqlVariant);
        }
        return stringBuilder.ToString();
    }

    public static void BuildDropScript(TextWriter writer, SqlVariant sqlVariant)
    {
        writer.Write(ReadResource(sqlVariant, "Drop"));
    }

    public static string BuildDropScript(SqlVariant sqlVariant)
    {
        var stringBuilder = new StringBuilder();
        using (var stringWriter = new StringWriter(stringBuilder))
        {
            BuildDropScript(stringWriter, sqlVariant);
        }
        return stringBuilder.ToString();
    }

    static string ReadResource(SqlVariant sqlVariant, string prefix)
    {
        using (var stream = typeof(SubscriptionScriptBuilder).Assembly.GetManifestResourceStream($"NServiceBus.Bridge.SubscriptionStorage.Scripts.{prefix}_{sqlVariant}.sql"))
        using (var reader = new StreamReader(stream))
        {
            return reader.ReadToEnd();
        }
    }
}
