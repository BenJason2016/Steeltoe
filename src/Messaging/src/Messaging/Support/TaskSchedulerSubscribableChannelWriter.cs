// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;

namespace Steeltoe.Messaging.Support;

public class TaskSchedulerSubscribableChannelWriter : AbstractMessageChannelWriter
{
    public virtual TaskSchedulerSubscribableChannel Channel => (TaskSchedulerSubscribableChannel)channel;

    public TaskSchedulerSubscribableChannelWriter(TaskSchedulerSubscribableChannel channel, ILogger logger = null)
        : base(channel, logger)
    {
    }

    public override bool TryComplete(Exception error = null)
    {
        return false;
    }

    public override bool TryWrite(IMessage message)
    {
        return channel.Send(message, 0);
    }

    public override ValueTask<bool> WaitToWriteAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new ValueTask<bool>(Task.FromCanceled<bool>(cancellationToken));
        }

        if (Channel.SubscriberCount > 0)
        {
            return new ValueTask<bool>(true);
        }

        return new ValueTask<bool>(false);
    }
}
