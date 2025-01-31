// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using System.Text;
using Steeltoe.Messaging;
using Steeltoe.Messaging.Converter;
using Steeltoe.Stream.Configuration;
using Xunit;

namespace Steeltoe.Stream.Test.Configuration;

public sealed class ArgumentResolversTest
{
    [Fact]
    public void TestSmartPayloadArgumentResolver()
    {
        var resolver = new SmartPayloadArgumentResolver(new TestMessageConverter());
        byte[] payload = Encoding.UTF8.GetBytes("hello");
        IMessage<byte[]> message = Message.Create(payload);
        ParameterInfo parameter = GetType().GetMethod(nameof(ByteArray)).GetParameters()[0];
        object resolvedArgument = resolver.ResolveArgument(parameter, message);
        Assert.Same(payload, resolvedArgument);

        parameter = GetType().GetMethod(nameof(Object)).GetParameters()[0];
        resolvedArgument = resolver.ResolveArgument(parameter, message);
        Assert.True(resolvedArgument is IMessage);

        var payload2 = new Dictionary<object, object>();
        IMessage<Dictionary<object, object>> message2 = Message.Create(payload2);
        parameter = GetType().GetMethod(nameof(Dict)).GetParameters()[0];
        resolvedArgument = resolver.ResolveArgument(parameter, message2);
        Assert.Same(payload2, resolvedArgument);

        parameter = GetType().GetMethod(nameof(Object)).GetParameters()[0];
        resolvedArgument = resolver.ResolveArgument(parameter, message2);
        Assert.True(resolvedArgument is IMessage);
    }

    private sealed class TestMessageConverter : IMessageConverter
    {
        public const string DefaultServiceName = nameof(TestMessageConverter);

        public string ServiceName { get; set; } = DefaultServiceName;

        public object FromMessage(IMessage message, Type targetType)
        {
            return message;
        }

        public T FromMessage<T>(IMessage message)
        {
            return (T)message;
        }

        public IMessage ToMessage(object payload, IMessageHeaders headers)
        {
            return Message.Create(payload);
        }
    }

#pragma warning disable xUnit1013 // Public method should be marked as test
    public void ByteArray(byte[] p)
    {
    }

    public void ByteArrayMessage(IMessage<byte[]> p)
    {
    }

    public void Object(object p)
    {
    }

    public void Dict(Dictionary<object, object> p)
    {
    }
#pragma warning restore xUnit1013 // Public method should be marked as test
}
