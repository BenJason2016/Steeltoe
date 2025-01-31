// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Steeltoe.Common.Lifecycle;
using Steeltoe.Connectors;
using Steeltoe.Connectors.RabbitMQ;
using Steeltoe.Messaging.RabbitMQ.Configuration;
using Steeltoe.Messaging.RabbitMQ.Hosting;
using Xunit;
using RC = RabbitMQ.Client;

namespace Steeltoe.Messaging.RabbitMQ.Test.Host;

public sealed class RabbitMQHostTest
{
    [Fact]
    public void HostCanBeStarted()
    {
        MockRabbitHostedService hostedService;

        using (IHost host = RabbitMQHost.CreateDefaultBuilder().UseDefaultServiceProvider(options => options.ValidateScopes = true)
            .ConfigureServices(svc => svc.AddSingleton<IHostedService, MockRabbitHostedService>()).Start())
        {
            Assert.NotNull(host);
            hostedService = (MockRabbitHostedService)host.Services.GetRequiredService<IHostedService>();
            Assert.NotNull(hostedService);
            Assert.Equal(1, hostedService.StartCount);
            Assert.Equal(0, hostedService.StopCount);
            Assert.Equal(0, hostedService.DisposeCount);
        }

        Assert.Equal(1, hostedService.StartCount);
        Assert.Equal(0, hostedService.StopCount);
        Assert.Equal(1, hostedService.DisposeCount);
    }

    [Fact]
    public void HostShouldInitializeServices()
    {
        using IHost host = RabbitMQHost.CreateDefaultBuilder().UseDefaultServiceProvider(options => options.ValidateScopes = true).Start();
        var lifecycleProcessor = host.Services.GetRequiredService<ILifecycleProcessor>();
        var rabbitHostService = (RabbitHostService)host.Services.GetRequiredService<IHostedService>();

        Assert.True(lifecycleProcessor.IsRunning);
        Assert.NotNull(rabbitHostService);
    }

    [Fact]
    public void HostShouldAddRabbitOptionsConfiguration()
    {
        IHostBuilder hostBuilder = RabbitMQHost.CreateDefaultBuilder().UseDefaultServiceProvider(options => options.ValidateScopes = true);

        var appSettings = new Dictionary<string, string>
        {
            [$"{RabbitOptions.Prefix}:host"] = "ThisIsATest",
            [$"{RabbitOptions.Prefix}:port"] = "1234",
            [$"{RabbitOptions.Prefix}:username"] = "TestUser",
            [$"{RabbitOptions.Prefix}:password"] = "TestPassword"
        };

        hostBuilder.ConfigureAppConfiguration(configBuilder =>
        {
            configBuilder.AddInMemoryCollection(appSettings);
        });

        using IHost host = hostBuilder.Start();
        RabbitOptions rabbitOptions = host.Services.GetService<IOptions<RabbitOptions>>()?.Value;

        Assert.NotNull(rabbitOptions);
        Assert.Equal("ThisIsATest", rabbitOptions.Host);
        Assert.Equal(1234, rabbitOptions.Port);
        Assert.Equal("TestUser", rabbitOptions.Username);
        Assert.Equal("TestPassword", rabbitOptions.Password);
    }

    [Fact]
    public void HostShouldSendCommandLineArgs()
    {
        IHostBuilder hostBuilder = RabbitMQHost.CreateDefaultBuilder(new[]
        {
            "RabbitHostCommandKey=RabbitHostCommandValue"
        }).UseDefaultServiceProvider(options => options.ValidateScopes = true);

        using IHost host = hostBuilder.Start();
        var configuration = host.Services.GetService<IConfiguration>();

        Assert.Equal("RabbitHostCommandValue", configuration["RabbitHostCommandKey"]);
    }

    [Fact]
    public void ShouldWorkWithRabbitMQConnector()
    {
        IHostBuilder builder = RabbitMQHost.CreateDefaultBuilder().UseDefaultServiceProvider(options => options.ValidateScopes = true);
        builder.ConfigureAppConfiguration(configurationBuilder => configurationBuilder.ConfigureRabbitMQ());
        builder.ConfigureServices((context, services) => services.AddRabbitMQ(context.Configuration));

        using IHost host = builder.Start();
        var connectorFactory = host.Services.GetRequiredService<ConnectorFactory<RabbitMQOptions, RC.IConnection>>();

        Assert.NotNull(connectorFactory);
    }
}
