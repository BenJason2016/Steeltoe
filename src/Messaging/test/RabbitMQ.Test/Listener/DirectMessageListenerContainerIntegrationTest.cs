// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using Steeltoe.Common.RetryPolly;
using Steeltoe.Messaging.RabbitMQ.Configuration;
using Steeltoe.Messaging.RabbitMQ.Connection;
using Steeltoe.Messaging.RabbitMQ.Core;
using Steeltoe.Messaging.RabbitMQ.Listener;
using Steeltoe.Messaging.RabbitMQ.Listener.Adapters;
using Xunit;
using Xunit.Abstractions;
using static Steeltoe.Messaging.RabbitMQ.Test.Attributes.EnableRabbitIntegrationTest;

namespace Steeltoe.Messaging.RabbitMQ.Test.Listener;

[Trait("Category", "Integration")]
public sealed class DirectMessageListenerContainerIntegrationTest : IDisposable
{
    public const string Q1 = "testQ1.DirectMessageListenerContainerIntegrationTests";
    public const string Q2 = "testQ2.DirectMessageListenerContainerIntegrationTests";
    public const string Eq1 = "eventTestQ1.DirectMessageListenerContainerIntegrationTests";
    public const string Eq2 = "eventTestQ2.DirectMessageListenerContainerIntegrationTests";
    public const string Dlq1 = "testDLQ1.DirectMessageListenerContainerIntegrationTests";

    private static int _testNumber = 1;

    private readonly CachingConnectionFactory _adminCf;

    private readonly RabbitAdmin _admin;

    private readonly string _testName;

    private readonly ITestOutputHelper _output;

    public DirectMessageListenerContainerIntegrationTest(ITestOutputHelper output)
    {
        _adminCf = new CachingConnectionFactory("localhost");
        _admin = new RabbitAdmin(_adminCf);
        _admin.DeclareQueue(new Queue(Q1));
        _admin.DeclareQueue(new Queue(Q2));
        _testName = $"DirectMessageListenerContainerIntegrationTest-{_testNumber++}";
        _output = output;
    }

    [Fact]
    public async Task TestDirect()
    {
        var cf = new CachingConnectionFactory("localhost");
        var container = new DirectMessageListenerContainer(null, cf);
        container.SetQueueNames(Q1, Q2);
        container.ConsumersPerQueue = 2;
        var listener = new ReplyingMessageListener();
        var adapter = new MessageListenerAdapter(null, listener);
        container.MessageListener = adapter;
        container.ServiceName = "simple";
        container.ConsumerTagStrategy = new TestConsumerTagStrategy(_testName);
        await container.StartAsync();
        Assert.True(container.StartedLatch.Wait(TimeSpan.FromSeconds(10)));
        var template = new RabbitTemplate(cf);
        Assert.Equal("FOO", template.ConvertSendAndReceive<string>(Q1, "foo"));
        Assert.Equal("BAR", template.ConvertSendAndReceive<string>(Q2, "bar"));
        await container.StopAsync();
        Assert.True(await ConsumersOnQueueAsync(Q1, 0));
        Assert.True(await ConsumersOnQueueAsync(Q2, 0));
        Assert.True(await ActiveConsumerCountAsync(container, 0));
        Assert.Empty(container.ConsumersByQueue);
        await template.StopAsync();
        cf.Destroy();
    }

    [Fact]
    public async Task TestMaxAttemptsRetryOnThrow()
    {
        var cf = new CachingConnectionFactory("localhost");
        int maxAttempts = 3;

        var container = new DirectMessageListenerContainer(null, cf)
        {
            RetryTemplate = new PollyRetryTemplate(maxAttempts, 1, 1, 1),
            Recoverer = new DefaultReplyRecoveryCallback()
        };

        container.SetQueueNames(Q1);

        var listener = new ThrowingMessageListener();
        var adapter = new MessageListenerAdapter(null, listener);
        container.MessageListener = adapter;
        container.ServiceName = "simple";
        container.ConsumerTagStrategy = new TestConsumerTagStrategy(_testName);
        await container.StartAsync();
        Assert.True(container.StartedLatch.Wait(TimeSpan.FromSeconds(10)));
        var template = new RabbitTemplate(cf);
        template.ConvertSendAndReceive<string>(Q1, "foo");

        Assert.Equal(maxAttempts, listener.ExceptionCounter);

        await container.StopAsync();
        await template.StopAsync();
        cf.Destroy();
    }

    public void Dispose()
    {
        _admin.DeleteQueue(Q1);
        _admin.DeleteQueue(Q2);
        _adminCf.Dispose();
    }

    private async Task<bool> ConsumersOnQueueAsync(string queue, int expected)
    {
        int n = 0;
        int currentQueueCount = -1;
        _output.WriteLine(queue + " waiting for " + expected);

        while (n++ < 600)
        {
            Dictionary<string, object> queueProperties = _admin.GetQueueProperties(queue);

            if (queueProperties != null && queueProperties.TryGetValue(RabbitAdmin.QueueConsumerCount, out object count))
            {
                currentQueueCount = (int)(uint)count;
            }

            if (currentQueueCount == expected)
            {
                return true;
            }

            await Task.Delay(100);

            _output.WriteLine(queue + " waiting for " + expected + " : " + currentQueueCount);
        }

        return currentQueueCount == expected;
    }

    private async Task<bool> ActiveConsumerCountAsync(DirectMessageListenerContainer container, int expected)
    {
        int n = 0;
        List<DirectMessageListenerContainer.SimpleConsumer> consumers = container.Consumers;

        while (n++ < 600 && consumers.Count != expected)
        {
            await Task.Delay(100);
        }

        return consumers.Count == expected;
    }

    private sealed class ReplyingMessageListener : IReplyingMessageListener<string, string>
    {
        public string HandleMessage(string input)
        {
            if (input == "foo" || input == "bar")
            {
                return input.ToUpperInvariant();
            }

            return null;
        }
    }

    private sealed class ThrowingMessageListener : IReplyingMessageListener<string, string>
    {
        public int ExceptionCounter { get; private set; }

        public ThrowingMessageListener(int exceptionCounter = 0)
        {
            ExceptionCounter = exceptionCounter;
        }

        public string HandleMessage(string input)
        {
            ExceptionCounter++;
            throw new InvalidOperationException("Intentional exception to test retry");
        }
    }

    private sealed class TestConsumerTagStrategy : IConsumerTagStrategy
    {
        private readonly string _testName;
        private int _n;

        public string ServiceName { get; set; } = nameof(TestConsumerTagStrategy);

        public TestConsumerTagStrategy(string testName)
        {
            _testName = testName;
        }

        public string CreateConsumerTag(string queue)
        {
            return $"{queue}/{_testName}{_n++}";
        }
    }
}
