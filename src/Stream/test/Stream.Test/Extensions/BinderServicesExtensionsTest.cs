// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Steeltoe.Common.Contexts;
using Steeltoe.Stream.Binder;
using Steeltoe.Stream.Configuration;
using Steeltoe.Stream.Extensions;
using Xunit;

namespace Steeltoe.Stream.Test.Extensions;

public sealed class BinderServicesExtensionsTest
{
    [Fact]
    public void AddBinderServices_AddsServices()
    {
        var container = new ServiceCollection();
        container.AddOptions();
        container.AddLogging(b => b.AddConsole());
        IConfigurationRoot configurationRoot = new ConfigurationBuilder().Build();
        container.AddSingleton<IConfiguration>(configurationRoot);
        container.AddSingleton<IApplicationContext, GenericApplicationContext>();
        container.AddBinderServices(configurationRoot);
        ServiceProvider serviceProvider = container.BuildServiceProvider(true);

        Assert.NotNull(serviceProvider.GetService<IBinderFactory>());
        Assert.NotNull(serviceProvider.GetService<IBinderTypeRegistry>());
        Assert.NotNull(serviceProvider.GetService<IBinderConfigurations>());
    }
}
