using System;
using System.IO;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NUnit.Framework;

public static class ConfigureEndpointLearningTransport
{
    public static TransportExtensions<TestTransport> Configure(this TransportExtensions<TestTransport> transportConfig)
    {
        var testRunId = TestContext.CurrentContext.Test.ID;

        string tempDir;

        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            //can't use bin dir since that will be too long on the build agents
            tempDir = @"c:\temp";
        }
        else
        {
            tempDir = Path.GetTempPath();
        }

        var storageDir = Path.Combine(tempDir, testRunId);

        transportConfig.StorageDirectory(storageDir);
        transportConfig.NoNativePubSub();
        return transportConfig;
    }

    public static Task DeleteQueue(RunSummary summary, string queue)
    {
        return Task.FromResult(0);
    }
}