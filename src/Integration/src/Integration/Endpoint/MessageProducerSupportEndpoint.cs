// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;
using Steeltoe.Common;
using Steeltoe.Common.Contexts;
using Steeltoe.Common.Util;
using Steeltoe.Integration.Support;
using Steeltoe.Messaging;
using Steeltoe.Messaging.Support;

namespace Steeltoe.Integration.Endpoint;

public abstract class MessageProducerSupportEndpoint : AbstractEndpoint, IMessageProducer
{
    private readonly MessagingTemplate _messagingTemplate;
    private readonly object _lock = new();

    private IErrorMessageStrategy _errorMessageStrategy = new DefaultErrorMessageStrategy();

    private volatile IMessageChannel _outputChannel;

    private volatile string _outputChannelName;

    private volatile IMessageChannel _errorChannel;

    private volatile string _errorChannelName;

    protected virtual MessagingTemplate MessagingTemplate => _messagingTemplate;

    public virtual IMessageChannel OutputChannel
    {
        get
        {
            if (_outputChannelName != null)
            {
                lock (_lock)
                {
                    if (_outputChannelName != null)
                    {
                        _outputChannel = IntegrationServices.ChannelResolver.ResolveDestination(_outputChannelName);
                    }

                    _outputChannelName = null;
                }
            }

            return _outputChannel;
        }
        set => _outputChannel = value;
    }

    public virtual string OutputChannelName
    {
        get => _outputChannelName;
        set
        {
            ArgumentGuard.NotNullOrEmpty(value);

            _outputChannelName = value;
        }
    }

    public virtual IMessageChannel ErrorChannel
    {
        get
        {
            if (_errorChannelName != null)
            {
                lock (_lock)
                {
                    if (_errorChannelName != null)
                    {
                        _errorChannel = IntegrationServices.ChannelResolver.ResolveDestination(_errorChannelName);
                    }

                    _errorChannelName = null;
                }
            }

            return _errorChannel;
        }
        set => _errorChannel = value;
    }

    public virtual string ErrorChannelName
    {
        get => _errorChannelName;
        set
        {
            ArgumentGuard.NotNullOrEmpty(value);

            _errorChannelName = value;
        }
    }

    public virtual int SendTimeout
    {
        get => _messagingTemplate.SendTimeout;
        set => _messagingTemplate.SendTimeout = value;
    }

    public virtual IErrorMessageStrategy ErrorMessageStrategy
    {
        get => _errorMessageStrategy;
        set
        {
            ArgumentGuard.NotNull(value);

            _errorMessageStrategy = value;
        }
    }

    protected MessageProducerSupportEndpoint(IApplicationContext context, ILogger logger = null)
        : base(context)
    {
        Phase = int.MaxValue / 2;
        _messagingTemplate = new MessagingTemplate(context, logger);
    }

    protected internal virtual void SendMessage(IMessage messageArg)
    {
        IMessage message = messageArg;

        if (message == null)
        {
            throw new MessagingException("cannot send a null message");
        }

        try
        {
            if (OutputChannel == null)
            {
                throw new InvalidOperationException("The 'outputChannel' or `outputChannelName` must be configured");
            }

            _messagingTemplate.Send(OutputChannel, message);
        }
        catch (Exception e)
        {
            if (!SendErrorMessageIfNecessary(message, e))
            {
                throw;
            }
        }
    }

    protected override Task DoStartAsync()
    {
        return Task.CompletedTask;
    }

    protected override Task DoStopAsync()
    {
        return Task.CompletedTask;
    }

    protected bool SendErrorMessageIfNecessary(IMessage message, Exception exception)
    {
        IMessageChannel channel = ErrorChannel;

        if (channel != null)
        {
            _messagingTemplate.Send(channel, BuildErrorMessage(message, exception));
            return true;
        }

        return false;
    }

    protected ErrorMessage BuildErrorMessage(IMessage message, Exception exception)
    {
        return _errorMessageStrategy.BuildErrorMessage(exception, GetErrorMessageAttributes(message));
    }

    protected virtual IAttributeAccessor GetErrorMessageAttributes(IMessage message)
    {
        return ErrorMessageUtils.GetAttributeAccessor(message, null);
    }
}
