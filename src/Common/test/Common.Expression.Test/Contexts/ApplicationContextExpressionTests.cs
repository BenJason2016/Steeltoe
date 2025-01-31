// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Steeltoe.Common.Contexts;
using Steeltoe.Common.Expression.Internal.Contexts;
using Steeltoe.Common.Services;
using Xunit;

namespace Steeltoe.Common.Expression.Test.Contexts;

public sealed class ApplicationContextExpressionTests
{
    private readonly IServiceProvider _serviceProvider;

    public ApplicationContextExpressionTests()
    {
        IConfigurationRoot configurationRoot = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            { "code", "123" }
        }).Build();

        var collection = new ServiceCollection();
        collection.AddSingleton<IConfiguration>(configurationRoot);

        collection.AddSingleton(_ =>
        {
            var tb = new TestService
            {
                ServiceName = "tb0"
            };

            return tb;
        });

        collection.AddSingleton(_ =>
        {
            var tb = new TestService
            {
                ServiceName = "tb1"
            };

            return tb;
        });

        collection.AddSingleton<IApplicationContext>(p =>
        {
            var context = new GenericApplicationContext(p, configurationRoot)
            {
                ServiceExpressionResolver = new StandardServiceExpressionResolver()
            };

            return context;
        });

        _serviceProvider = collection.BuildServiceProvider(true);
    }

    [Fact]
    public void GenericApplicationContext()
    {
        var context = _serviceProvider.GetService<IApplicationContext>();
        IEnumerable<TestService> services = context.GetServices<TestService>();
        Assert.Equal(2, services.Count());
        Assert.Equal("XXXtb0YYYZZZ", Evaluate("XXX#{tb0.Name}YYYZZZ"));
        Assert.Equal("123", Evaluate("${code}"));
        Assert.Equal("123", Evaluate("${code?#{null}}"));
        Assert.Null(Evaluate("${codeX?#{null}}"));
        Assert.Equal("123 tb1", Evaluate("${code} #{tb1.Name}"));
        Assert.Equal("foo tb1", Evaluate("${bar?foo} #{tb1.Name}"));
    }

    private object Evaluate(string value)
    {
        var context = _serviceProvider.GetService<IApplicationContext>();
        string result = context.ResolveEmbeddedValue(value);
        return context.ServiceExpressionResolver.Evaluate(result, new ServiceExpressionContext(context));
    }

    public sealed class TestService : IServiceNameAware
    {
        public string Name => ServiceName;

        public string ServiceName { get; set; } = "NotSet";
    }

    public sealed class ValueTestService : IServiceNameAware
    {
        public string ServiceName { get; set; } = "tb3";
    }
}
