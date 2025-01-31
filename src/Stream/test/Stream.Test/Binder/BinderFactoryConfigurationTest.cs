// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Steeltoe.Messaging;
using Steeltoe.Stream.Binder;
using Xunit;

namespace Steeltoe.Stream.Test.Binder;

public sealed class BinderFactoryConfigurationTest : AbstractTest
{
    [Fact]
    public void LoadBinderTypeRegistryWithOneBinder()
    {
        List<string> searchDirectories = GetSearchDirectories("StubBinder1");
        ServiceProvider provider = CreateStreamsContainer(searchDirectories, "spring:cloud:stream:defaultBinder=binder1").BuildServiceProvider(true);
        var typeRegistry = provider.GetService<IBinderTypeRegistry>();
        Assert.Single(typeRegistry.GetAll());

        Assert.Contains("binder1", typeRegistry.GetAll().Keys);
        var factory = provider.GetService<IBinderFactory>();
        Assert.NotNull(factory);
        IBinder binder1 = factory.GetBinder("binder1", typeof(IMessageChannel));
        Assert.NotNull(binder1);

        IBinder defaultBinder = factory.GetBinder(null, typeof(IMessageChannel));
        Assert.Same(binder1, defaultBinder);
    }

    [Fact]
    public void LoadBinderTypeRegistryWithOneBinderWithBinderSpecificConfigurationData()
    {
        List<string> searchDirectories = GetSearchDirectories("StubBinder1");
        ServiceProvider provider = CreateStreamsContainer(searchDirectories, "binder1:name=foo").BuildServiceProvider(true);

        var factory = provider.GetService<IBinderFactory>();

        _ = factory.GetBinder("binder1", typeof(IMessageChannel));

        IConfigurationSection section = provider.GetService<IConfiguration>().GetSection("binder1");
        Assert.Equal("foobar", section["name"]);
    }

    [Fact]
    public void LoadBinderTypeRegistryWithTwoBinders()
    {
        List<string> searchDirectories = GetSearchDirectories("StubBinder1", "StubBinder2");
        ServiceProvider provider = CreateStreamsContainer(searchDirectories).BuildServiceProvider(true);

        var typeRegistry = provider.GetService<IBinderTypeRegistry>();

        Assert.NotNull(typeRegistry);
        Assert.Equal(2, typeRegistry.GetAll().Count);
        Assert.Contains("binder1", typeRegistry.GetAll().Keys);
        Assert.Contains("binder2", typeRegistry.GetAll().Keys);
        var factory = provider.GetService<IBinderFactory>();
        Assert.NotNull(factory);
        Assert.Throws<InvalidOperationException>(() => factory.GetBinder(null, typeof(IMessageChannel)));

        IBinder binder1 = factory.GetBinder("binder1", typeof(IMessageChannel));
        Assert.NotNull(binder1);
        IBinder binder2 = factory.GetBinder("binder2", typeof(IMessageChannel));
        Assert.NotNull(binder1);
        Assert.NotSame(binder1, binder2);
    }

    [Fact]
    public void LoadBinderTypeRegistryWithCustomNonDefaultCandidate()
    {
        List<string> searchDirectories = GetSearchDirectories("StubBinder1", "TestBinder");

        ServiceProvider provider = CreateStreamsContainer(searchDirectories,
            "spring.cloud.stream.binders.custom.configureclass=" + "Steeltoe.Stream.TestBinder.Startup, Steeltoe.Stream.TestBinder",
            "spring.cloud.stream.binders.custom.defaultCandidate=false", "spring.cloud.stream.binders.custom.inheritEnvironment=false",
            "spring.cloud.stream.defaultbinder=binder1").BuildServiceProvider(true);

        var typeRegistry = provider.GetService<IBinderTypeRegistry>();
        Assert.NotNull(typeRegistry);
        Assert.Equal(2, typeRegistry.GetAll().Count);
        Assert.Contains("binder1", typeRegistry.GetAll().Keys);
        var factory = provider.GetService<IBinderFactory>();
        Assert.NotNull(factory);

        IBinder defaultBinder = factory.GetBinder(null, typeof(IMessageChannel));
        Assert.NotNull(defaultBinder);
        Assert.Equal("binder1", defaultBinder.ServiceName);

        IBinder binder1 = factory.GetBinder("binder1", typeof(IMessageChannel));
        Assert.NotNull(binder1);
        Assert.Equal("binder1", binder1.ServiceName);

        Assert.Same(binder1, defaultBinder);
    }

    [Fact]
    public void LoadDefaultBinderWithTwoBinders()
    {
        List<string> searchDirectories = GetSearchDirectories("StubBinder1", "StubBinder2");
        ServiceProvider provider = CreateStreamsContainer(searchDirectories, "spring.cloud.stream.defaultBinder=binder2").BuildServiceProvider(true);
        var typeRegistry = provider.GetService<IBinderTypeRegistry>();
        Assert.NotNull(typeRegistry);
        Assert.Equal(2, typeRegistry.GetAll().Count);
        Assert.Contains("binder1", typeRegistry.GetAll().Keys);
        Assert.Contains("binder2", typeRegistry.GetAll().Keys);

        var factory = provider.GetService<IBinderFactory>();
        Assert.NotNull(factory);

        IBinder binder1 = factory.GetBinder("binder1", typeof(IMessageChannel));
        Assert.Equal("binder1", binder1.ServiceName);
        IBinder binder2 = factory.GetBinder("binder2", typeof(IMessageChannel));
        Assert.Equal("binder2", binder2.ServiceName);
        IBinder defaultBinder = factory.GetBinder(null, typeof(IMessageChannel));
        Assert.Same(defaultBinder, binder2);
    }
}
