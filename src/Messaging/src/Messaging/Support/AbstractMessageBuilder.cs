// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using Steeltoe.Common;

namespace Steeltoe.Messaging.Support;

public abstract class AbstractMessageBuilder
{
    protected readonly object Payload;

    protected readonly IMessage OriginalMessage;

    protected MessageHeaderAccessor headerAccessor;

    protected AbstractMessageBuilder()
    {
    }

    protected AbstractMessageBuilder(IMessage message)
    {
        ArgumentGuard.NotNull(message);

        Payload = message.Payload;
        OriginalMessage = message;
        headerAccessor = new MessageHeaderAccessor(message);
    }

    protected AbstractMessageBuilder(MessageHeaderAccessor accessor)
    {
        ArgumentGuard.NotNull(accessor);

        Payload = null;
        OriginalMessage = null;
        headerAccessor = accessor;
    }

    protected AbstractMessageBuilder(object payload, MessageHeaderAccessor accessor)
    {
        ArgumentGuard.NotNull(payload);
        ArgumentGuard.NotNull(accessor);

        Payload = payload;
        OriginalMessage = null;
        headerAccessor = accessor;
    }

    public abstract AbstractMessageBuilder SetHeaders(MessageHeaderAccessor accessor);

    public abstract AbstractMessageBuilder SetHeader(string headerName, object headerValue);

    public abstract AbstractMessageBuilder SetHeaderIfAbsent(string headerName, object headerValue);

    public abstract AbstractMessageBuilder RemoveHeaders(params string[] headerPatterns);

    public abstract AbstractMessageBuilder RemoveHeader(string headerName);

    public abstract AbstractMessageBuilder CopyHeaders(IDictionary<string, object> headersToCopy);

    public abstract AbstractMessageBuilder CopyHeadersIfAbsent(IDictionary<string, object> headersToCopy);

    public abstract AbstractMessageBuilder SetReplyChannel(IMessageChannel replyChannel);

    public abstract AbstractMessageBuilder SetReplyChannelName(string replyChannelName);

    public abstract AbstractMessageBuilder SetErrorChannel(IMessageChannel errorChannel);

    public abstract AbstractMessageBuilder SetErrorChannelName(string errorChannelName);

    public virtual IMessage Build()
    {
        if (OriginalMessage != null && !headerAccessor.IsModified)
        {
            return OriginalMessage;
        }

        IMessageHeaders headersToUse = headerAccessor.ToMessageHeaders();
        return Message.Create(Payload, headersToUse, Payload.GetType());
    }
}
