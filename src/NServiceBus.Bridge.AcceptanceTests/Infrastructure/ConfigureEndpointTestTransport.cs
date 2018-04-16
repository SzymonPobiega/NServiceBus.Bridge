using System;
using System.IO;
using NServiceBus;
using NUnit.Framework;

public static class ConfigureEndpointTestTransport
{
    public static TransportExtensions<TestTransport> ConfigureNoNativePubSubBrokerA(this TransportExtensions<TestTransport> transportConfig)
    {
        Configure(transportConfig, "A");
        transportConfig.NoNativePubSub();
        return transportConfig;
    }

    public static TransportExtensions<TestTransport> ConfigureNativePubSubBrokerB(this TransportExtensions<TestTransport> transportConfig)
    {
        Configure(transportConfig, "B");
        return transportConfig;
    }

    public static TransportExtensions<TestTransport> ConfigureNativePubSubBrokerC(this TransportExtensions<TestTransport> transportConfig)
    {
        Configure(transportConfig, "C");
        return transportConfig;
    }

    static void Configure(TransportExtensions<TestTransport> transportConfig, string brokerId)
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

        var storageDir = Path.Combine(tempDir, testRunId, brokerId);

        transportConfig.StorageDirectory(storageDir);
    }
}