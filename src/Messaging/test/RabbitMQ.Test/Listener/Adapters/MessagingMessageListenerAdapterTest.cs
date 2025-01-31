// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using Moq;
using Steeltoe.Common.Util;
using Steeltoe.Messaging.Converter;
using Steeltoe.Messaging.Handler.Attributes.Support;
using Steeltoe.Messaging.Handler.Invocation;
using Steeltoe.Messaging.RabbitMQ.Extensions;
using Steeltoe.Messaging.RabbitMQ.Listener.Adapters;
using Steeltoe.Messaging.RabbitMQ.Listener.Exceptions;
using Steeltoe.Messaging.RabbitMQ.Support;
using Steeltoe.Messaging.RabbitMQ.Support.Converter;
using Steeltoe.Messaging.RabbitMQ.Test.Test;
using Xunit;
using RC = RabbitMQ.Client;

namespace Steeltoe.Messaging.RabbitMQ.Test.Listener.Adapters;

public sealed class MessagingMessageListenerAdapterTest : AbstractTest
{
    private readonly DefaultMessageHandlerMethodFactory _factory = new();
    private readonly SampleBean _sample = new();

    [Fact]
    public void BuildMessageWithStandardMessage()
    {
        IMessage result = RabbitMessageBuilder.WithPayload("Response").SetHeader("foo", "bar").SetHeader(RabbitMessageHeaders.Type, "msg_type")
            .SetHeader(RabbitMessageHeaders.ReplyTo, "reply").Build();

        var session = new Mock<RC.IModel>();
        MessagingMessageListenerAdapter listener = GetSimpleInstance(nameof(SampleBean.Echo), typeof(IMessage<string>));
        IMessage<byte[]> replyMessage = listener.BuildMessage(session.Object, result, null);
        Assert.NotNull(replyMessage);
        Assert.Equal("Response", EncodingUtils.GetDefaultEncoding().GetString(replyMessage.Payload));
        Assert.Equal("msg_type", replyMessage.Headers.Type());
        Assert.Equal("reply", replyMessage.Headers.ReplyTo());
        Assert.Equal("bar", replyMessage.Headers.Get<string>("foo"));
    }

    [Fact]
    public void ExceptionInListener()
    {
        IMessage<byte[]> message = MessageTestUtils.CreateTextMessage("foo");
        var mockChannel = new Mock<RC.IModel>();
        mockChannel.Setup(c => c.CreateBasicProperties()).Returns(new MockRabbitBasicProperties());
        MessagingMessageListenerAdapter listener = GetSimpleInstance(nameof(SampleBean.Fail), typeof(string));

        try
        {
            listener.OnMessage(message, mockChannel.Object);
            throw new Exception("Should have thrown an exception");
        }
        catch (ListenerExecutionFailedException ex)
        {
            Assert.IsType<ArgumentException>(ex.InnerException);
            Assert.Contains("Expected test exception", ex.InnerException.Message, StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            throw new Exception($"Should not have thrown an {ex}");
        }
    }

    [Fact]
    public void ExceptionInListenerBadReturnExceptionSetting()
    {
        IMessage<byte[]> message = MessageTestUtils.CreateTextMessage("foo");
        var mockChannel = new Mock<RC.IModel>();
        mockChannel.Setup(c => c.CreateBasicProperties()).Returns(new MockRabbitBasicProperties());
        MessagingMessageListenerAdapter listener = GetSimpleInstance("Fail", true, typeof(string));

        try
        {
            listener.OnMessage(message, mockChannel.Object);
            throw new Exception("Should have thrown an exception");
        }
        catch (ListenerExecutionFailedException ex)
        {
            Assert.IsType<ArgumentException>(ex.InnerException);
            Assert.Contains("Expected test exception", ex.InnerException.Message, StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            throw new Exception($"Should not have thrown an {ex}");
        }
    }

    [Fact]
    public void ExceptionInMultiListenerReturnException()
    {
        IMessage<byte[]> message = MessageTestUtils.CreateTextMessage("foo");
        var mockChannel = new Mock<RC.IModel>();
        mockChannel.Setup(c => c.CreateBasicProperties()).Returns(new MockRabbitBasicProperties());

        MessagingMessageListenerAdapter listener =
            GetMultiInstance(nameof(SampleBean.Fail), nameof(SampleBean.FailWithReturn), true, typeof(string), typeof(byte[]));

        try
        {
            listener.OnMessage(message, mockChannel.Object);
            throw new Exception("Should have thrown an exception");
        }
        catch (ListenerExecutionFailedException ex)
        {
            Assert.IsType<ArgumentException>(ex.InnerException);
            Assert.Contains("Expected test exception", ex.InnerException.Message, StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            throw new Exception($"Should not have thrown an {ex}");
        }

        message = Message.Create(new byte[]
        {
            1,
            2
        }, new MessageHeaders());

        try
        {
            listener.OnMessage(message, mockChannel.Object);
            throw new Exception("Should have thrown an exception");
        }
        catch (ReplyFailureException ex)
        {
            Assert.Contains("Failed to send reply", ex.Message, StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            throw new Exception($"Should not have thrown an {ex}");
        }

        RabbitHeaderAccessor accessor = RabbitHeaderAccessor.GetMutableAccessor(message);
        accessor.ReplyTo = "foo/bar";
        listener.OnMessage(message, mockChannel.Object);
    }

    [Fact]
    public void ExceptionInInvocation()
    {
        IMessage<byte[]> message = MessageTestUtils.CreateTextMessage("foo");
        var mockChannel = new Mock<RC.IModel>();
        mockChannel.Setup(c => c.CreateBasicProperties()).Returns(new MockRabbitBasicProperties());
        MessagingMessageListenerAdapter listener = GetSimpleInstance(nameof(SampleBean.WrongParam), typeof(int));

        try
        {
            listener.OnMessage(message, mockChannel.Object);
            throw new Exception("Should have thrown an exception");
        }
        catch (ListenerExecutionFailedException ex)
        {
            Assert.IsType<MessageConversionException>(ex.InnerException);
        }
        catch (Exception ex)
        {
            throw new Exception($"Should not have thrown an {ex}");
        }
    }

    [Fact]
    public void GenericMessageTest1()
    {
        IMessage<byte[]> message = MessageTestUtils.CreateTextMessage("\"foo\"");
        var mockChannel = new Mock<RC.IModel>();
        mockChannel.Setup(c => c.CreateBasicProperties()).Returns(new MockRabbitBasicProperties());
        MessagingMessageListenerAdapter listener = GetSimpleInstance(nameof(SampleBean.WithGenericMessageObjectType), typeof(IMessage<object>));
        listener.MessageConverter = new JsonMessageConverter();
        RabbitHeaderAccessor accessor = RabbitHeaderAccessor.GetMutableAccessor(message);
        accessor.ContentType = MimeTypeUtils.ApplicationJsonValue;
        listener.OnMessage(message, mockChannel.Object);
        Assert.IsType<string>(_sample.Payload);
    }

    [Fact]
    public void GenericMessageTest2()
    {
        IMessage<byte[]> message = MessageTestUtils.CreateTextMessage("{ \"foostring\" : \"bar\" }");
        var mockChannel = new Mock<RC.IModel>();
        mockChannel.Setup(c => c.CreateBasicProperties()).Returns(new MockRabbitBasicProperties());
        MessagingMessageListenerAdapter listener = GetSimpleInstance(nameof(SampleBean.WithGenericMessageFooType), typeof(IMessage<Foo>));
        listener.MessageConverter = new JsonMessageConverter();
        RabbitHeaderAccessor accessor = RabbitHeaderAccessor.GetMutableAccessor(message);
        accessor.ContentType = MimeTypeUtils.ApplicationJsonValue;
        listener.OnMessage(message, mockChannel.Object);
        Assert.IsType<Foo>(_sample.Payload);
    }

    [Fact]
    public void GenericMessageTest3()
    {
        IMessage<byte[]> message = MessageTestUtils.CreateTextMessage("\"foo\"");
        var mockChannel = new Mock<RC.IModel>();
        mockChannel.Setup(c => c.CreateBasicProperties()).Returns(new MockRabbitBasicProperties());
        MessagingMessageListenerAdapter listener = GetSimpleInstance(nameof(SampleBean.WithNonGenericMessage), typeof(IMessage));
        listener.MessageConverter = new JsonMessageConverter();
        RabbitHeaderAccessor accessor = RabbitHeaderAccessor.GetMutableAccessor(message);
        accessor.ContentType = MimeTypeUtils.ApplicationJsonValue;
        listener.OnMessage(message, mockChannel.Object);
        Assert.IsType<string>(_sample.Payload);
    }

    [Fact]
    public void GenericMessageTest4()
    {
        IMessage<byte[]> message = MessageTestUtils.CreateTextMessage("{ \"foo\" : \"bar\" }");
        var mockChannel = new Mock<RC.IModel>();
        mockChannel.Setup(c => c.CreateBasicProperties()).Returns(new MockRabbitBasicProperties());

        MessagingMessageListenerAdapter listener =
            GetSimpleInstance(nameof(SampleBean.WithGenericMessageDictionaryType), typeof(IMessage<Dictionary<string, string>>));

        listener.MessageConverter = new JsonMessageConverter();
        RabbitHeaderAccessor accessor = RabbitHeaderAccessor.GetMutableAccessor(message);
        accessor.ContentType = MimeTypeUtils.ApplicationJsonValue;
        listener.OnMessage(message, mockChannel.Object);
        Assert.IsType<Dictionary<string, string>>(_sample.Payload);
    }

    [Fact]
    public void BatchMessagesTest()
    {
        IMessage<byte[]> message = MessageTestUtils.CreateTextMessage("{ \"foo1\" : \"bar1\" }");
        var mockChannel = new Mock<RC.IModel>();
        mockChannel.Setup(c => c.CreateBasicProperties()).Returns(new MockRabbitBasicProperties());
        BatchMessagingMessageListenerAdapter listener = GetBatchInstance(nameof(SampleBean.WithMessageBatch), typeof(List<IMessage<byte[]>>));
        listener.MessageConverter = new JsonMessageConverter();
        RabbitHeaderAccessor accessor = RabbitHeaderAccessor.GetMutableAccessor(message);
        accessor.ContentType = MimeTypeUtils.ApplicationJsonValue;

        listener.OnMessageBatch(new List<IMessage>
        {
            message
        }, mockChannel.Object);

        Assert.IsType<string>(_sample.BatchPayloads[0]);
    }

    [Fact]
    public void BatchTypedMessagesTest()
    {
        IMessage<byte[]> message = MessageTestUtils.CreateTextMessage("{ \"foostring\" : \"bar1\" }");
        var mockChannel = new Mock<RC.IModel>();
        mockChannel.Setup(c => c.CreateBasicProperties()).Returns(new MockRabbitBasicProperties());
        BatchMessagingMessageListenerAdapter listener = GetBatchInstance(nameof(SampleBean.WithTypedMessageBatch), typeof(List<IMessage<Foo>>));
        listener.MessageConverter = new JsonMessageConverter();
        RabbitHeaderAccessor accessor = RabbitHeaderAccessor.GetMutableAccessor(message);
        accessor.ContentType = MimeTypeUtils.ApplicationJsonValue;

        listener.OnMessageBatch(new List<IMessage>
        {
            message
        }, mockChannel.Object);

        Assert.IsType<Foo>(_sample.BatchPayloads[0]);
    }

    [Fact]
    public void BatchTypedObjectTest()
    {
        IMessage<byte[]> message = MessageTestUtils.CreateTextMessage("{ \"foostring\" : \"bar1\" }");
        var mockChannel = new Mock<RC.IModel>();
        mockChannel.Setup(c => c.CreateBasicProperties()).Returns(new MockRabbitBasicProperties());
        BatchMessagingMessageListenerAdapter listener = GetBatchInstance(nameof(SampleBean.WithFooBatch), typeof(List<Foo>));
        listener.MessageConverter = new JsonMessageConverter();
        RabbitHeaderAccessor accessor = RabbitHeaderAccessor.GetMutableAccessor(message);
        accessor.ContentType = MimeTypeUtils.ApplicationJsonValue;

        listener.OnMessageBatch(new List<IMessage>
        {
            message
        }, mockChannel.Object);

        Assert.IsType<Foo>(_sample.BatchPayloads[0]);
    }

    private MessagingMessageListenerAdapter GetSimpleInstance(string methodName, params Type[] parameterTypes)
    {
        MethodInfo m = typeof(SampleBean).GetMethod(methodName, parameterTypes);
        return CreateInstance(m, false);
    }

    private MessagingMessageListenerAdapter GetSimpleInstance(string methodName, bool returnExceptions, params Type[] parameterTypes)
    {
        MethodInfo m = typeof(SampleBean).GetMethod(methodName, parameterTypes);
        return CreateInstance(m, returnExceptions);
    }

    private MessagingMessageListenerAdapter GetMultiInstance(string methodName1, string methodName2, bool returnExceptions, Type m1ParameterType,
        Type m2ParameterType)
    {
        MethodInfo m1 = typeof(SampleBean).GetMethod(methodName1, new[]
        {
            m1ParameterType
        });

        MethodInfo m2 = typeof(SampleBean).GetMethod(methodName2, new[]
        {
            m2ParameterType
        });

        return CreateMultiInstance(m1, m2, returnExceptions);
    }

    private MessagingMessageListenerAdapter CreateMultiInstance(MethodInfo m1, MethodInfo m2, bool returnExceptions)
    {
        var adapter = new MessagingMessageListenerAdapter(null, null, null, returnExceptions, null);

        var methods = new List<IInvocableHandlerMethod>
        {
            _factory.CreateInvocableHandlerMethod(_sample, m1),
            _factory.CreateInvocableHandlerMethod(_sample, m2)
        };

        var handler = new DelegatingInvocableHandler(methods, _sample, null, null);
        adapter.HandlerAdapter = new HandlerAdapter(handler);
        return adapter;
    }

    private MessagingMessageListenerAdapter CreateInstance(MethodInfo m, bool returnExceptions)
    {
        var adapter = new MessagingMessageListenerAdapter(null, null, m, returnExceptions, null)
        {
            HandlerAdapter = new HandlerAdapter(_factory.CreateInvocableHandlerMethod(_sample, m))
        };

        return adapter;
    }

    private BatchMessagingMessageListenerAdapter GetBatchInstance(string methodName, params Type[] parameterTypes)
    {
        MethodInfo m = typeof(SampleBean).GetMethod(methodName, parameterTypes);
        return CreateBatchInstance(m);
    }

    private BatchMessagingMessageListenerAdapter CreateBatchInstance(MethodInfo m)
    {
        var adapter = new BatchMessagingMessageListenerAdapter(null, null, m, false, null, null)
        {
            HandlerAdapter = new HandlerAdapter(_factory.CreateInvocableHandlerMethod(_sample, m))
        };

        return adapter;
    }

#pragma warning disable S1172 // Unused method parameters should be removed
    private sealed class SampleBean
    {
        public object Payload { get; private set; }
        public List<object> BatchPayloads { get; private set; }

        public IMessage<string> Echo(IMessage<string> input)
        {
            return (IMessage<string>)RabbitMessageBuilder.WithPayload(input.Payload).SetHeader(RabbitMessageHeaders.Type, "reply").Build();
        }

        public void Fail(string input)
        {
            throw new ArgumentException("Expected test exception");
        }

        public void WrongParam(int i)
        {
            throw new ArgumentException("Should not have been called");
        }

        public void WithGenericMessageObjectType(IMessage<object> message)
        {
            Payload = message.Payload;
        }

        public void WithGenericMessageFooType(IMessage<Foo> message)
        {
            Payload = message.Payload;
        }

        public void WithGenericMessageDictionaryType(IMessage<Dictionary<string, string>> message)
        {
            Payload = message.Payload;
        }

        public void WithNonGenericMessage(IMessage message)
        {
            Payload = message.Payload;
        }

        public void WithMessageBatch(List<IMessage<byte[]>> messageBatch)
        {
            BatchPayloads = new List<object>();

            foreach (IMessage<byte[]> m in messageBatch)
            {
                BatchPayloads.Add(EncodingUtils.GetDefaultEncoding().GetString(m.Payload));
            }
        }

        public void WithTypedMessageBatch(List<IMessage<Foo>> messageBatch)
        {
            BatchPayloads = new List<object>();

            foreach (IMessage<Foo> m in messageBatch)
            {
                BatchPayloads.Add(m.Payload);
            }
        }

        public void WithFooBatch(List<Foo> messageBatch)
        {
            BatchPayloads = new List<object>();

            foreach (Foo m in messageBatch)
            {
                BatchPayloads.Add(m);
            }
        }

        public string FailWithReturn(byte[] input)
        {
            throw new ArgumentException("Expected test exception");
        }
    }
#pragma warning restore S1172 // Unused method parameters should be removed

    private sealed class Foo
    {
    }
}
