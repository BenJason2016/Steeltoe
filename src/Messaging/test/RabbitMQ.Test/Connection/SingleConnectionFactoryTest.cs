// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using Moq;
using Steeltoe.Common.Util;
using Steeltoe.Messaging.RabbitMQ.Connection;
using Xunit;
using RC = RabbitMQ.Client;

namespace Steeltoe.Messaging.RabbitMQ.Test.Connection;

public sealed class SingleConnectionFactoryTest : AbstractConnectionFactoryTest
{
    [Fact]
    public void TestWithChannelListener()
    {
        var mockConnectionFactory = new Mock<RC.IConnectionFactory>();
        var mockConnection = new Mock<RC.IConnection>();
        var mockChannel = new Mock<RC.IModel>();

        mockConnectionFactory.Setup(f => f.CreateConnection(It.IsAny<string>())).Returns(mockConnection.Object);
        mockConnection.Setup(c => c.IsOpen).Returns(true);
        mockConnection.Setup(c => c.CreateModel()).Returns(mockChannel.Object);

        var called = new AtomicInteger(0);
        AbstractConnectionFactory connectionFactory = CreateConnectionFactory(mockConnectionFactory.Object);
        var listener = new TestListener(called);

        connectionFactory.SetChannelListeners(new List<IChannelListener>
        {
            listener
        });

        IConnection con = connectionFactory.CreateConnection();
        RC.IModel channel = con.CreateChannel();
        Assert.Equal(1, called.Value);

        channel.Close();
        con.Close();

        mockConnection.Verify(c => c.Close(), Times.Never);

        con = connectionFactory.CreateConnection();
        con.CreateChannel();
        Assert.Equal(2, called.Value);

        connectionFactory.Destroy();
        mockConnection.Verify(c => c.Close(It.IsAny<int>()), Times.AtLeastOnce);
        mockConnectionFactory.Verify(c => c.CreateConnection(It.IsAny<string>()));
    }

    protected override AbstractConnectionFactory CreateConnectionFactory(RC.IConnectionFactory connectionFactory)
    {
        var scf = new SingleConnectionFactory(connectionFactory, null);
        return scf;
    }

    private sealed class TestListener : IChannelListener
    {
        private readonly AtomicInteger _called;

        public TestListener(AtomicInteger called)
        {
            _called = called;
        }

        public void OnCreate(RC.IModel channel, bool transactional)
        {
            _called.IncrementAndGet();
        }

        public void OnShutDown(RC.ShutdownEventArgs args)
        {
            throw new NotImplementedException();
        }
    }
}
