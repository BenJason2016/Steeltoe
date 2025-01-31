// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;
using Moq;
using Steeltoe.Common.Contexts;
using Steeltoe.Common.Retry;
using Steeltoe.Common.RetryPolly;
using Steeltoe.Common.Util;
using Steeltoe.Messaging.RabbitMQ.Core;
using Steeltoe.Messaging.RabbitMQ.Extensions;
using Steeltoe.Messaging.RabbitMQ.Listener.Adapters;
using Steeltoe.Messaging.RabbitMQ.Support;
using Xunit;
using RC = RabbitMQ.Client;

namespace Steeltoe.Messaging.RabbitMQ.Test.Listener.Adapters;

public sealed class MessageListenerAdapterTest
{
    private readonly SimpleService _simpleService = new();

    private readonly MessageHeaders _messageProperties;

    private MessageListenerAdapter _adapter;

    public MessageListenerAdapterTest()
    {
        var headers = new Dictionary<string, object>
        {
            { MessageHeaders.ContentType, MimeTypeUtils.TextPlainValue }
        };

        _messageProperties = new MessageHeaders(headers);
        _adapter = new MessageListenerAdapter(null);
    }

    [Fact]
    public void TestExtendedListenerAdapter()
    {
        var extendedAdapter = new ExtendedListenerAdapter(null);
        var called = new AtomicBoolean(false);
        var channelMock = new Mock<RC.IModel>();
        var testDelegate = new TestDelegate(called);
        extendedAdapter.Instance = testDelegate;
        extendedAdapter.ContainerAckMode = AcknowledgeMode.Manual;
        byte[] bytes = EncodingUtils.GetDefaultEncoding().GetBytes("foo");
        extendedAdapter.OnMessage(Message.Create(bytes, _messageProperties), channelMock.Object);
        Assert.True(called.Value);
    }

    [Fact]
    public void TestDefaultListenerMethod()
    {
        var called = new AtomicBoolean(false);
        var delegate1 = new TestDelegate1(called);
        _adapter.Instance = delegate1;
        byte[] bytes = EncodingUtils.GetDefaultEncoding().GetBytes("foo");
        _adapter.OnMessage(Message.Create(bytes, _messageProperties), null);
        Assert.True(called.Value);
    }

    [Fact]
    public void TestAlternateConstructor()
    {
        var called = new AtomicBoolean(false);
        var delegate2 = new TestDelegate2(called);
        _adapter = new MessageListenerAdapter(null, delegate2, nameof(TestDelegate2.MyPojoMessageMethod));
        byte[] bytes = EncodingUtils.GetDefaultEncoding().GetBytes("foo");
        _adapter.OnMessage(Message.Create(bytes, _messageProperties), null);
        Assert.True(called.Value);
    }

    [Fact]
    public void TestExplicitListenerMethod()
    {
        _adapter.DefaultListenerMethod = "Handle";
        _adapter.Instance = _simpleService;
        byte[] bytes = EncodingUtils.GetDefaultEncoding().GetBytes("foo");
        _adapter.OnMessage(Message.Create(bytes, _messageProperties), null);
        Assert.Equal("Handle", _simpleService.Called);
    }

    [Fact]
    public void TestMappedListenerMethod()
    {
        var map = new Dictionary<string, string>
        {
            { "foo", "Handle" },
            { "bar", "NotDefinedOnInterface" }
        };

        _adapter.DefaultListenerMethod = "AnotherHandle";
        _adapter.SetQueueOrTagToMethodName(map);
        _adapter.Instance = _simpleService;
        byte[] bytes = EncodingUtils.GetDefaultEncoding().GetBytes("foo");
        IMessage<byte[]> message = Message.Create(bytes, _messageProperties);
        RabbitHeaderAccessor accessor = RabbitHeaderAccessor.GetMutableAccessor(message);
        accessor.ConsumerQueue = "foo";
        accessor.ConsumerTag = "bar";
        _adapter.OnMessage(message, null);
        Assert.Equal("Handle", _simpleService.Called);
        message = Message.Create(bytes, _messageProperties);
        accessor = RabbitHeaderAccessor.GetMutableAccessor(message);
        accessor.ConsumerQueue = "junk";
        _adapter.OnMessage(message, null);
        Assert.Equal("NotDefinedOnInterface", _simpleService.Called);
        message = Message.Create(bytes, _messageProperties);
        accessor = RabbitHeaderAccessor.GetMutableAccessor(message);
        accessor.ConsumerTag = "junk";
        _adapter.OnMessage(message, null);
        Assert.Equal("AnotherHandle", _simpleService.Called);
    }

    [Fact]
    public void TestReplyRetry()
    {
        _adapter.DefaultListenerMethod = "Handle";
        _adapter.Instance = _simpleService;
        _adapter.RetryTemplate = new PollyRetryTemplate(new Dictionary<Type, bool>(), 2, true, 1, 1, 1);
        var replyMessage = new AtomicReference<IMessage>();
        var replyAddress = new AtomicReference<Address>();
        var throwable = new AtomicReference<Exception>();
        _adapter.RecoveryCallback = new TestRecoveryCallback(replyMessage, replyAddress, throwable);

        RabbitHeaderAccessor accessor = RabbitHeaderAccessor.GetMutableAccessor(_messageProperties);
        accessor.ReplyTo = "foo/bar";
        var ex = new Exception();
        var mockChannel = new Mock<RC.IModel>();
        mockChannel.Setup(c => c.BasicPublish("foo", "bar", false, It.IsAny<RC.IBasicProperties>(), It.IsAny<byte[]>())).Throws(ex);
        mockChannel.Setup(c => c.CreateBasicProperties()).Returns(new MockRabbitBasicProperties());
        byte[] bytes = EncodingUtils.GetDefaultEncoding().GetBytes("foo");
        IMessage<byte[]> message = Message.Create(bytes, _messageProperties);
        _adapter.OnMessage(message, mockChannel.Object);
        Assert.Equal("Handle", _simpleService.Called);
        Assert.NotNull(replyMessage.Value);
        Assert.NotNull(replyAddress.Value);
        Address address = replyAddress.Value;
        Assert.Equal("foo", address.ExchangeName);
        Assert.Equal("bar", address.RoutingKey);
        Assert.Same(ex, throwable.Value);
    }

    [Fact]
    public void TestTaskReturn()
    {
        var called = new CountdownEvent(1);
        var @delegate = new TestAsyncDelegate();

        _adapter = new MessageListenerAdapter(null, @delegate, nameof(TestAsyncDelegate.MyPojoMessageMethodAsync))
        {
            ContainerAckMode = AcknowledgeMode.Manual,
            ResponseExchange = "default"
        };

        var mockChannel = new Mock<RC.IModel>();
        mockChannel.Setup(c => c.CreateBasicProperties()).Returns(new MockRabbitBasicProperties());
        mockChannel.Setup(c => c.BasicAck(It.IsAny<ulong>(), false)).Callback(() => called.Signal());
        byte[] bytes = EncodingUtils.GetDefaultEncoding().GetBytes("foo");
        IMessage<byte[]> message = Message.Create(bytes, _messageProperties);
        _adapter.OnMessage(message, mockChannel.Object);
        Assert.True(called.Wait(TimeSpan.FromSeconds(10)));
    }

    public sealed class TestRecoveryCallback : IRecoveryCallback
    {
        private readonly AtomicReference<IMessage> _replyMessage;
        private readonly AtomicReference<Address> _replyAddress;
        private readonly AtomicReference<Exception> _throwable;

        public TestRecoveryCallback(AtomicReference<IMessage> replyMessage, AtomicReference<Address> replyAddress, AtomicReference<Exception> throwable)
        {
            _replyMessage = replyMessage;
            _replyAddress = replyAddress;
            _throwable = throwable;
        }

        public object Recover(IRetryContext context)
        {
            _replyMessage.Value = SendRetryContextAccessor.GetMessage(context);
            _replyAddress.Value = SendRetryContextAccessor.GetAddress(context);
            _throwable.Value = context.LastException;
            return null;
        }
    }

    public interface IService
    {
        string Handle(string input);

        string AnotherHandle(string input);
    }

    public sealed class SimpleService : IService
    {
        public string Called { get; private set; }

        public string Handle(string input)
        {
            Called = "Handle";
            return $"processed{input}";
        }

        public string AnotherHandle(string input)
        {
            Called = "AnotherHandle";
            return $"processed{input}";
        }

        public string NotDefinedOnInterface(string input)
        {
            Called = "NotDefinedOnInterface";
            return $"processed{input}";
        }
    }

    private sealed class TestAsyncDelegate
    {
        public Task<string> MyPojoMessageMethodAsync(string input)
        {
            return Task.Run(() =>
            {
                Thread.Sleep(1000);
                return $"processed{input}";
            });
        }
    }

    private sealed class TestDelegate2
    {
        private readonly AtomicBoolean _called;

        public TestDelegate2(AtomicBoolean called)
        {
            _called = called;
        }

        public string MyPojoMessageMethod(string input)
        {
            _called.Value = true;
            return $"processed{input}";
        }
    }

    private sealed class TestDelegate1
    {
        private readonly AtomicBoolean _called;

        public TestDelegate1(AtomicBoolean called)
        {
            _called = called;
        }

#pragma warning disable S1144 // Unused private types or members should be removed
        public string HandleMessage(string input)
        {
            _called.Value = true;
            return $"processed{input}";
        }
#pragma warning restore S1144 // Unused private types or members should be removed
    }

    private sealed class TestDelegate
    {
        private readonly AtomicBoolean _called;

        public TestDelegate(AtomicBoolean called)
        {
            _called = called;
        }

#pragma warning disable S1144 // Unused private types or members should be removed
        public void HandleMessage(string input, RC.IModel channel, IMessage message)
        {
            Assert.NotNull(input);
            Assert.NotNull(channel);
            Assert.NotNull(message);
            ulong deliveryTag = 0;

            if (message.Headers.DeliveryTag().HasValue)
            {
                deliveryTag = message.Headers.DeliveryTag().Value;
            }

            channel.BasicAck(deliveryTag, false);
            _called.Value = true;
        }
#pragma warning restore S1144 // Unused private types or members should be removed
    }

    private sealed class ExtendedListenerAdapter : MessageListenerAdapter
    {
        public ExtendedListenerAdapter(IApplicationContext context, ILogger logger = null)
            : base(context, logger)
        {
        }

        protected override object[] BuildListenerArguments(object extractedMessage, RC.IModel channel, IMessage message)
        {
            return new[]
            {
                extractedMessage,
                channel,
                message
            };
        }
    }
}
