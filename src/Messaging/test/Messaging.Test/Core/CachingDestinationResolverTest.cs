// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using Moq;
using Steeltoe.Messaging.Core;
using Xunit;

namespace Steeltoe.Messaging.Test.Core;

public sealed class CachingDestinationResolverTest
{
    [Fact]
    public void CachedDestination()
    {
        var resolverMock = new Mock<IDestinationResolver<string>>();
        IDestinationResolver<string> resolver = resolverMock.Object;
        var resolverProxy = new CachingDestinationResolverProxy<string>(resolver);
        resolverMock.Setup(r => r.ResolveDestination("abcd")).Returns("dcba");
        resolverMock.Setup(r => r.ResolveDestination("1234")).Returns("4321");

        Assert.Equal("dcba", resolverProxy.ResolveDestination("abcd"));
        Assert.Equal("4321", resolverProxy.ResolveDestination("1234"));
        Assert.Equal("4321", resolverProxy.ResolveDestination("1234"));
        Assert.Equal("dcba", resolverProxy.ResolveDestination("abcd"));

        resolverMock.Verify(r => r.ResolveDestination("abcd"), Times.Once);
        resolverMock.Verify(r => r.ResolveDestination("1234"), Times.Once);
    }

    [Fact]
    public void NullTargetThroughConstructor()
    {
        Assert.Throws<ArgumentNullException>(() => new CachingDestinationResolverProxy<string>(null));
    }
}
