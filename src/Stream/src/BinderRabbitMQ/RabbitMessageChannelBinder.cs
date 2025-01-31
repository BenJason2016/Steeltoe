// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Steeltoe.Common;
using Steeltoe.Common.Contexts;
using Steeltoe.Common.Expression.Internal;
using Steeltoe.Common.RetryPolly;
using Steeltoe.Common.Util;
using Steeltoe.Integration;
using Steeltoe.Integration.Acks;
using Steeltoe.Integration.RabbitMQ.Inbound;
using Steeltoe.Integration.RabbitMQ.Outbound;
using Steeltoe.Integration.RabbitMQ.Support;
using Steeltoe.Integration.Support;
using Steeltoe.Integration.Util;
using Steeltoe.Messaging;
using Steeltoe.Messaging.RabbitMQ;
using Steeltoe.Messaging.RabbitMQ.Batch;
using Steeltoe.Messaging.RabbitMQ.Configuration;
using Steeltoe.Messaging.RabbitMQ.Connection;
using Steeltoe.Messaging.RabbitMQ.Core;
using Steeltoe.Messaging.RabbitMQ.Exceptions;
using Steeltoe.Messaging.RabbitMQ.Extensions;
using Steeltoe.Messaging.RabbitMQ.Listener;
using Steeltoe.Messaging.RabbitMQ.Listener.Exceptions;
using Steeltoe.Messaging.RabbitMQ.Retry;
using Steeltoe.Messaging.RabbitMQ.Support;
using Steeltoe.Messaging.RabbitMQ.Support.Converter;
using Steeltoe.Messaging.RabbitMQ.Support.PostProcessor;
using Steeltoe.Stream.Binder.RabbitMQ.Configuration;
using Steeltoe.Stream.Binder.RabbitMQ.Provisioning;
using Steeltoe.Stream.Configuration;
using Steeltoe.Stream.Provisioning;
using IntegrationChannel = Steeltoe.Integration.Channel;
using MessagingSupport = Steeltoe.Messaging.Support;
using SteeltoeConnectionFactory = Steeltoe.Messaging.RabbitMQ.Connection.IConnectionFactory;

namespace Steeltoe.Stream.Binder.RabbitMQ;

public class RabbitMessageChannelBinder : AbstractPollableMessageSourceBinder
{
    private static readonly SimplePassThroughMessageConverter PassThoughConverter = new();
    private static readonly IMessageHeadersConverter InboundMessagePropertiesConverter = new DefaultBinderMessagePropertiesConverter();
    private static readonly RabbitMessageHeaderErrorMessageStrategy ErrorMessageStrategy = new();
    private static readonly Regex InterceptorNeededPattern = new("(Payload|#root|#this)");

    protected ILogger logger;

    protected RabbitExchangeQueueProvisioner ProvisioningProvider => (RabbitExchangeQueueProvisioner)InnerProvisioningProvider;

    public SteeltoeConnectionFactory ConnectionFactory { get; }

    public IOptionsMonitor<RabbitOptions> RabbitConnectionOptions { get; }

    public RabbitBinderOptions BinderOptions { get; }

    public RabbitBindingsOptions BindingsOptions { get; }

    public IMessagePostProcessor DecompressingPostProcessor { get; set; } = new DelegatingDecompressingPostProcessor();

    public IMessagePostProcessor CompressingPostProcessor { get; set; } = new GZipPostProcessor();

    public string[] AdminAddresses { get; set; }

    public string[] Nodes { get; set; }

    public bool Clustered => Nodes?.Length > 1;

    public override string ServiceName { get; set; }

    public RabbitMessageChannelBinder(IApplicationContext context, ILogger<RabbitMessageChannelBinder> logger, SteeltoeConnectionFactory connectionFactory,
        IOptionsMonitor<RabbitOptions> rabbitOptions, IOptionsMonitor<RabbitBinderOptions> binderOptions,
        IOptionsMonitor<RabbitBindingsOptions> bindingsOptions, RabbitExchangeQueueProvisioner provisioningProvider)
        : this(context, logger, connectionFactory, rabbitOptions, binderOptions, bindingsOptions, provisioningProvider, null, null)
    {
    }

    public RabbitMessageChannelBinder(IApplicationContext context, ILogger<RabbitMessageChannelBinder> logger, SteeltoeConnectionFactory connectionFactory,
        IOptionsMonitor<RabbitOptions> rabbitOptions, IOptionsMonitor<RabbitBinderOptions> binderOptions,
        IOptionsMonitor<RabbitBindingsOptions> bindingsOptions, RabbitExchangeQueueProvisioner provisioningProvider,
        IListenerContainerCustomizer containerCustomizer)
        : this(context, logger, connectionFactory, rabbitOptions, binderOptions, bindingsOptions, provisioningProvider, containerCustomizer, null)
    {
    }

    public RabbitMessageChannelBinder(IApplicationContext context, ILogger<RabbitMessageChannelBinder> logger, SteeltoeConnectionFactory connectionFactory,
        IOptionsMonitor<RabbitOptions> rabbitOptions, IOptionsMonitor<RabbitBinderOptions> binderOptions,
        IOptionsMonitor<RabbitBindingsOptions> bindingsOptions, RabbitExchangeQueueProvisioner provisioningProvider,
        IListenerContainerCustomizer containerCustomizer, IMessageSourceCustomizer sourceCustomizer)
        : base(context, Array.Empty<string>(), provisioningProvider, containerCustomizer, sourceCustomizer, logger)
    {
        ArgumentGuard.NotNull(connectionFactory);
        ArgumentGuard.NotNull(rabbitOptions);

        this.logger = logger;
        ConnectionFactory = connectionFactory;
        RabbitConnectionOptions = rabbitOptions;
        BinderOptions = binderOptions?.CurrentValue;
        BindingsOptions = bindingsOptions?.CurrentValue;
        ServiceName = "rabbitBinder";
    }

    public RabbitConsumerOptions GetConsumerOptions(string channelName)
    {
        return BindingsOptions.GetRabbitConsumerOptions(channelName);
    }

    public RabbitProducerOptions GetProducerOptions(string channelName)
    {
        return BindingsOptions.GetRabbitProducerOptions(channelName);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ConnectionFactory.Destroy();
        }

        base.Dispose(disposing);
    }

    protected override IMessageHandler CreateProducerMessageHandler(IProducerDestination destination, IProducerOptions producerProperties,
        IMessageChannel errorChannel)
    {
        if (producerProperties.HeaderMode == HeaderMode.EmbeddedHeaders)
        {
            throw new InvalidOperationException("The RabbitMQ binder does not support embedded headers since RabbitMQ supports headers natively");
        }

        RabbitProducerOptions extendedProperties = BindingsOptions.GetRabbitProducerOptions(producerProperties.BindingName);
        string prefix = extendedProperties.Prefix;
        string exchangeName = destination.Name;
        string destinationName = string.IsNullOrEmpty(prefix) ? exchangeName : exchangeName[prefix.Length..];
        RabbitTemplate template = BuildRabbitTemplate(extendedProperties, errorChannel != null || extendedProperties.UseConfirmHeader.GetValueOrDefault());

        var endpoint = new RabbitOutboundEndpoint(ApplicationContext, template, logger)
        {
            ExchangeName = exchangeName
        };

        bool expressionInterceptorNeeded = ExpressionInterceptorNeeded(extendedProperties);
        string routingKeyExpression = extendedProperties.RoutingKeyExpression;

        if (!producerProperties.IsPartitioned)
        {
            UpdateRoutingKeyExpressionForNonPartitioned(endpoint, destinationName, expressionInterceptorNeeded, routingKeyExpression);
        }
        else
        {
            UpdateRoutingKeyExpressionForPartitioned(destinationName, endpoint, expressionInterceptorNeeded, routingKeyExpression);
        }

        if (extendedProperties.DelayExpression != null)
        {
            if (expressionInterceptorNeeded)
            {
                endpoint.SetDelayExpressionString($"Headers['{RabbitExpressionEvaluatingInterceptor.DelayHeader}']");
            }
            else
            {
                endpoint.DelayExpression = ExpressionParser.ParseExpression(extendedProperties.DelayExpression);
            }
        }

        DefaultRabbitHeaderMapper mapper = DefaultRabbitHeaderMapper.GetOutboundMapper(logger);

        var headerPatterns = new List<string>(extendedProperties.HeaderPatterns.Count + 3)
        {
            $"!{BinderHeaders.PartitionHeader}",
            $"!{IntegrationMessageHeaderAccessor.SourceData}",
            $"!{IntegrationMessageHeaderAccessor.DeliveryAttempt}"
        };

        headerPatterns.AddRange(extendedProperties.HeaderPatterns);

        mapper.SetRequestHeaderNames(headerPatterns.ToArray());
        endpoint.HeaderMapper = mapper;

        endpoint.DefaultDeliveryMode = extendedProperties.DeliveryMode.Value;

        if (errorChannel != null)
        {
            CheckConnectionFactoryIsErrorCapable();
            endpoint.ReturnChannel = errorChannel;

            if (!extendedProperties.UseConfirmHeader.GetValueOrDefault())
            {
                endpoint.ConfirmNackChannel = errorChannel;

                string ackChannelBeanName = !string.IsNullOrEmpty(extendedProperties.ConfirmAckChannel)
                    ? extendedProperties.ConfirmAckChannel
                    : IntegrationContextUtils.NullChannelBeanName;

                if (ackChannelBeanName != IntegrationContextUtils.NullChannelBeanName &&
                    !ApplicationContext.ContainsService<IMessageChannel>(ackChannelBeanName))
                {
                    var ackChannel = new IntegrationChannel.DirectChannel(ApplicationContext);
                    ApplicationContext.Register(ackChannelBeanName, ackChannel);
                }

                endpoint.ConfirmAckChannelName = ackChannelBeanName;
                endpoint.SetConfirmCorrelationExpressionString("#root");
            }
            else
            {
                if (!string.IsNullOrEmpty(extendedProperties.ConfirmAckChannel))
                {
                    throw new InvalidOperationException("You cannot specify a 'confirmAckChannel' when 'useConfirmHeader' is true");
                }
            }

            endpoint.ErrorMessageStrategy = new DefaultErrorMessageStrategy();
        }

        endpoint.HeadersMappedLast = true;
        endpoint.Initialize();
        return endpoint;
    }

    protected override void PostProcessOutputChannel(IMessageChannel outputChannel, IProducerOptions producerOptions)
    {
        RabbitProducerOptions rabbitProducerOptions = BindingsOptions.GetRabbitProducerOptions(producerOptions.BindingName);

        if (ExpressionInterceptorNeeded(rabbitProducerOptions))
        {
            IExpression rkExpression = null;
            IExpression delayExpression = null;

            if (rabbitProducerOptions.RoutingKeyExpression != null)
            {
                rkExpression = ExpressionParser.ParseExpression(rabbitProducerOptions.RoutingKeyExpression);
            }

            if (rabbitProducerOptions.DelayExpression != null)
            {
                delayExpression = ExpressionParser.ParseExpression(rabbitProducerOptions.DelayExpression);
            }

            ((IntegrationChannel.AbstractMessageChannel)outputChannel).AddInterceptor(0,
                new RabbitExpressionEvaluatingInterceptor(rkExpression, delayExpression, EvaluationContext));
        }
    }

    protected override IMessageProducer CreateConsumerEndpoint(IConsumerDestination destination, string group, IConsumerOptions consumerOptions)
    {
        if (consumerOptions.HeaderMode == HeaderMode.EmbeddedHeaders)
        {
            throw new InvalidOperationException("The RabbitMQ binder does not support embedded headers since RabbitMQ supports headers natively");
        }

        string destinationName = destination.Name;

        RabbitConsumerOptions properties = BindingsOptions.GetRabbitConsumerOptions(consumerOptions.BindingName);

        var listenerContainer = new DirectMessageListenerContainer(ApplicationContext, ConnectionFactory)
        {
            AcknowledgeMode = properties.AcknowledgeMode.GetValueOrDefault(AcknowledgeMode.Auto),
            IsChannelTransacted = properties.Transacted.GetValueOrDefault(),
            DefaultRequeueRejected = properties.RequeueRejected ?? true
        };

        int concurrency = consumerOptions.Concurrency;
        concurrency = concurrency > 0 ? concurrency : 1;
        listenerContainer.ConsumersPerQueue = concurrency;
        listenerContainer.PrefetchCount = properties.Prefetch ?? listenerContainer.PrefetchCount;
        listenerContainer.RecoveryInterval = properties.RecoveryInterval ?? listenerContainer.RecoveryInterval;
        IEnumerable<string> queueNames = destinationName.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());
        listenerContainer.SetQueueNames(queueNames.ToArray());
        listenerContainer.SetAfterReceivePostProcessors(DecompressingPostProcessor);
        listenerContainer.MessageHeadersConverter = InboundMessagePropertiesConverter;
        listenerContainer.Exclusive = properties.Exclusive ?? listenerContainer.Exclusive;
        listenerContainer.MissingQueuesFatal = properties.MissingQueuesFatal ?? listenerContainer.MissingQueuesFatal;

        if (properties.FailedDeclarationRetryInterval != null)
        {
            listenerContainer.FailedDeclarationRetryInterval = properties.FailedDeclarationRetryInterval.Value;
        }

        ListenerContainerCustomizer?.Configure(listenerContainer, destination.Name, group);

        if (!string.IsNullOrEmpty(properties.ConsumerTagPrefix))
        {
            listenerContainer.ConsumerTagStrategy = new RabbitBinderConsumerTagStrategy(properties.ConsumerTagPrefix);
        }

        listenerContainer.Initialize();

        var adapter = new RabbitInboundChannelAdapter(ApplicationContext, listenerContainer, logger)
        {
            BindSourceMessage = true,
            ServiceName = $"inbound.{destinationName}"
        };

        ErrorInfrastructure errorInfrastructure = RegisterErrorInfrastructure(destination, group, consumerOptions, logger);

        if (consumerOptions.MaxAttempts > 1)
        {
            adapter.RetryTemplate = BuildRetryTemplate(consumerOptions);
            adapter.RecoveryCallback = errorInfrastructure.Recoverer;
        }
        else
        {
            adapter.ErrorMessageStrategy = ErrorMessageStrategy;
            adapter.ErrorChannel = errorInfrastructure.ErrorChannel;
        }

        adapter.MessageConverter = PassThoughConverter;
        return adapter;
    }

    protected override PolledConsumerResources CreatePolledConsumerResources(string name, string group, IConsumerDestination destination,
        IConsumerOptions consumerOptions)
    {
        if (consumerOptions.Multiplex)
        {
            throw new InvalidOperationException("The Polled MessageSource does not currently support multiple queues");
        }

        var source = new RabbitMessageSource(ApplicationContext, ConnectionFactory, destination.Name)
        {
            RawMessageHeader = true
        };

        MessageSourceCustomizer?.Configure(source, destination.Name, group);
        return new PolledConsumerResources(source, RegisterErrorInfrastructure(destination, group, consumerOptions, true, logger));
    }

    protected override void PostProcessPollableSource(DefaultPollableMessageSource bindingTarget)
    {
        bindingTarget.AttributeProvider = (accessor, message) =>
        {
            var rawMessage = message.Headers.Get<IMessage>(RabbitMessageHeaderErrorMessageStrategy.AmqpRawMessage);

            if (rawMessage != null)
            {
                accessor.SetAttribute(RabbitMessageHeaderErrorMessageStrategy.AmqpRawMessage, rawMessage);
            }
        };
    }

    protected override IErrorMessageStrategy GetErrorMessageStrategy()
    {
        return ErrorMessageStrategy;
    }

    protected override IMessageHandler GetErrorMessageHandler(IConsumerDestination destination, string group, IConsumerOptions consumerOptions)
    {
        RabbitConsumerOptions properties = BindingsOptions.GetRabbitConsumerOptions(consumerOptions.BindingName);

        if (properties.RepublishToDlq.Value)
        {
            return new RepublishToDlqErrorMessageHandler(this, properties, logger);
        }

        if (consumerOptions.MaxAttempts > 1)
        {
            return new RejectingErrorMessageHandler(logger);
        }

        return base.GetErrorMessageHandler(destination, group, consumerOptions);
    }

    protected override IMessageHandler GetPolledConsumerErrorMessageHandler(IConsumerDestination destination, string group, IConsumerOptions consumerProperties)
    {
        IMessageHandler handler = GetErrorMessageHandler(destination, group, consumerProperties);

        if (handler != null)
        {
            return handler;
        }

        IMessageHandler superHandler = base.GetErrorMessageHandler(destination, group, consumerProperties);
        RabbitConsumerOptions properties = BindingsOptions.GetRabbitConsumerOptions(consumerProperties.BindingName);
        return new DefaultPolledConsumerErrorMessageHandler(superHandler, properties, logger);
    }

    protected override string GetErrorsBaseName(IConsumerDestination destination, string group, IConsumerOptions consumerOptions)
    {
        return $"{destination.Name}.errors";
    }

    protected override void AfterUnbindConsumer(IConsumerDestination destination, string group, IConsumerOptions consumerOptions)
    {
        ProvisioningProvider.CleanAutoDeclareContext(destination, consumerOptions);
    }

    private static bool ExpressionInterceptorNeeded(RabbitProducerOptions extendedProperties)
    {
        string rkExpression = extendedProperties.RoutingKeyExpression;
        string delayExpression = extendedProperties.DelayExpression;

        return (rkExpression != null && InterceptorNeededPattern.IsMatch(rkExpression)) ||
            (delayExpression != null && InterceptorNeededPattern.IsMatch(delayExpression));
    }

    private void UpdateRoutingKeyExpressionForPartitioned(string destinationName, RabbitOutboundEndpoint endpoint, bool expressionInterceptorNeeded,
        string routingKeyExpression)
    {
        if (routingKeyExpression == null)
        {
            endpoint.RoutingKeyExpression = BuildPartitionRoutingExpression(destinationName, false);
        }
        else
        {
            endpoint.RoutingKeyExpression = expressionInterceptorNeeded
                ? BuildPartitionRoutingExpression($"Headers['{RabbitExpressionEvaluatingInterceptor.RoutingKeyHeader}']", true)
                : BuildPartitionRoutingExpression(routingKeyExpression, true);
        }
    }

    private void UpdateRoutingKeyExpressionForNonPartitioned(RabbitOutboundEndpoint endpoint, string destinationName, bool expressionInterceptorNeeded,
        string routingKeyExpression)
    {
        if (routingKeyExpression == null)
        {
            endpoint.RoutingKey = destinationName;
        }
        else
        {
            if (expressionInterceptorNeeded)
            {
                endpoint.SetRoutingKeyExpressionString($"Headers['{RabbitExpressionEvaluatingInterceptor.RoutingKeyHeader}']");
            }
            else
            {
                endpoint.RoutingKeyExpression = ExpressionParser.ParseExpression(routingKeyExpression);
            }
        }
    }

    private void CheckConnectionFactoryIsErrorCapable()
    {
        if (ConnectionFactory is not CachingConnectionFactory factory)
        {
            logger.LogWarning("Unknown connection factory type, cannot determine error capabilities: {type}", ConnectionFactory.GetType());
        }
        else
        {
            CachingConnectionFactory ccf = factory;

            if (!ccf.IsPublisherConfirms && !ccf.IsPublisherReturns)
            {
                logger.LogWarning("Producer error channel is enabled, but the connection factory is not configured for " +
                    "returns or confirms; the error channel will receive no messages");
            }
            else if (!ccf.IsPublisherConfirms)
            {
                logger.LogInformation("Producer error channel is enabled, but the connection factory is only configured to " +
                    "handle returned messages; negative acks will not be reported");
            }
            else if (!ccf.IsPublisherReturns)
            {
                logger.LogInformation("Producer error channel is enabled, but the connection factory is only configured to " +
                    "handle negatively acked messages; returned messages will not be reported");
            }
        }
    }

    private IExpression BuildPartitionRoutingExpression(string expressionRoot, bool rootIsExpression)
    {
        string partitionRoutingExpression = rootIsExpression
            ? $"{expressionRoot} + '-' + Headers['{BinderHeaders.PartitionHeader}']"
            : $"'{expressionRoot}-' + Headers['{BinderHeaders.PartitionHeader}']";

        return ExpressionParser.ParseExpression(partitionRoutingExpression);
    }

    private RabbitTemplate BuildRabbitTemplate(RabbitProducerOptions properties, bool mandatory)
    {
        RabbitTemplate rabbitTemplate;

        if (properties.BatchingEnabled.Value)
        {
            var batchingStrategy = new SimpleBatchingStrategy(properties.BatchSize.Value, properties.BatchBufferLimit.Value, properties.BatchTimeout.Value);
            rabbitTemplate = new BatchingRabbitTemplate(RabbitConnectionOptions, null, batchingStrategy);
        }
        else
        {
            rabbitTemplate = new RabbitTemplate();
        }

        rabbitTemplate.MessageConverter = PassThoughConverter;
        rabbitTemplate.IsChannelTransacted = properties.Transacted.Value;
        rabbitTemplate.ConnectionFactory = ConnectionFactory;
        rabbitTemplate.UsePublisherConnection = true;

        if (properties.Compress.Value)
        {
            rabbitTemplate.SetBeforePublishPostProcessors(CompressingPostProcessor);
        }

        rabbitTemplate.Mandatory = mandatory; // returned messages

        if (RabbitConnectionOptions != null && RabbitConnectionOptions.CurrentValue.Template.Retry.Enabled)
        {
            RabbitOptions.RetryOptions retry = RabbitConnectionOptions.CurrentValue.Template.Retry;

            var retryTemplate = new PollyRetryTemplate(retry.MaxAttempts, (int)retry.InitialInterval.TotalMilliseconds,
                (int)retry.MaxInterval.TotalMilliseconds, retry.Multiplier, logger);

            rabbitTemplate.RetryTemplate = retryTemplate;
        }

        return rabbitTemplate;
    }

    private sealed class DefaultPolledConsumerErrorMessageHandler : IMessageHandler
    {
        private readonly IMessageHandler _superHandler;
        private readonly RabbitConsumerOptions _properties;
        private readonly ILogger _logger;

        public string ServiceName { get; set; }

        public DefaultPolledConsumerErrorMessageHandler(IMessageHandler superHandler, RabbitConsumerOptions properties, ILogger logger)
        {
            _superHandler = superHandler;
            _properties = properties;
            _logger = logger;
            ServiceName = $"{GetType()}@{GetHashCode()}";
        }

        public void HandleMessage(IMessage message)
        {
            var amqpMessage = message.Headers.Get<IMessage>(RabbitMessageHeaderErrorMessageStrategy.AmqpRawMessage);

            if (message is not MessagingSupport.ErrorMessage errorMessage)
            {
                _logger?.LogError("Expected an ErrorMessage, not a {type} for: {message}", message.GetType(), message);
            }
            else if (amqpMessage == null)
            {
                if (_superHandler != null)
                {
                    _superHandler.HandleMessage(message);
                }
            }
            else
            {
                if (errorMessage.Payload is MessagingException payload)
                {
                    IAcknowledgmentCallback ack = StaticMessageHeaderAccessor.GetAcknowledgmentCallback(payload.FailedMessage);

                    if (ack != null)
                    {
                        ack.Acknowledge(_properties.RequeueRejected.Value ? Status.Requeue : Status.Reject);
                    }
                }
            }
        }
    }

    private sealed class RejectingErrorMessageHandler : IMessageHandler
    {
        private readonly RejectAndDoNotRequeueRecoverer _recoverer = new();
        private readonly ILogger _logger;

        public string ServiceName { get; set; }

        public RejectingErrorMessageHandler(ILogger logger)
        {
            ServiceName = $"{GetType()}@{GetHashCode()}";
            _logger = logger;
        }

        public void HandleMessage(IMessage message)
        {
            if (message is not MessagingSupport.ErrorMessage errorMessage)
            {
                _logger?.LogError("Expected an ErrorMessage, not a {type} for: {message}", message.GetType(), message);
                throw new ListenerExecutionFailedException($"Unexpected error message {message}", new RabbitRejectAndDoNotRequeueException(string.Empty), null);
            }

            _recoverer.Recover(errorMessage, errorMessage.Payload);
        }
    }

    private sealed class RepublishToDlqErrorMessageHandler : IMessageHandler
    {
        private readonly RabbitMessageChannelBinder _binder;
        private readonly RabbitTemplate _template;
        private readonly string _exchange;
        private readonly string _routingKey;
        private readonly int _frameMaxHeadroom;
        private readonly RabbitConsumerOptions _properties;
        private readonly ILogger _logger;
        private int _maxStackTraceLength = -1;

        public string ServiceName { get; set; }

        public RepublishToDlqErrorMessageHandler(RabbitMessageChannelBinder binder, RabbitConsumerOptions properties, ILogger logger)
        {
            _binder = binder;

            _template = new RabbitTemplate(_binder.ConnectionFactory)
            {
                UsePublisherConnection = true
            };

            _exchange = GetDeadLetterExchangeName(properties);
            _routingKey = properties.DeadLetterRoutingKey;
            _frameMaxHeadroom = properties.FrameMaxHeadroom.Value;
            _properties = properties;
            _logger = logger;
            ServiceName = $"{GetType()}@{GetHashCode()}";
        }

        public void HandleMessage(IMessage message)
        {
            if (message.Headers[IntegrationMessageHeaderAccessor.SourceData] is not IMessage errorMessage)
            {
                _logger?.LogError("Expected an ErrorMessage, not a {type} for: {message}", message.GetType(), message);
                return;
            }

            var cause = message.Payload as Exception;

            if (!ShouldRepublish(cause))
            {
                _logger.LogDebug("Skipping republish of: {message}", message);
                return;
            }

            string stackTraceAsString = cause.StackTrace;
            RabbitHeaderAccessor accessor = RabbitHeaderAccessor.GetMutableAccessor(errorMessage);

            if (_maxStackTraceLength < 0)
            {
                int result = RabbitUtils.GetMaxFrame(_binder.ConnectionFactory);

                if (result > 0)
                {
                    _maxStackTraceLength = result - _frameMaxHeadroom;
                }
            }

            if (_maxStackTraceLength > 0 && stackTraceAsString.Length > _maxStackTraceLength)
            {
                stackTraceAsString = stackTraceAsString.Substring(0, _maxStackTraceLength);

                _logger.LogWarning(cause,
                    "Stack trace in republished message header truncated due to frame_max limitations; consider increasing frame_max on the broker or reduce the stack trace depth");
            }

            accessor.SetHeader(RepublishMessageRecoverer.XExceptionStacktrace, stackTraceAsString);
            accessor.SetHeader(RepublishMessageRecoverer.XExceptionMessage, cause.InnerException != null ? cause.InnerException.Message : cause.Message);
            accessor.SetHeader(RepublishMessageRecoverer.XOriginalExchange, accessor.ReceivedExchange);
            accessor.SetHeader(RepublishMessageRecoverer.XOriginalRoutingKey, accessor.ReceivedRoutingKey);

            if (_properties.RepublishDeliveryMode != null)
            {
                accessor.DeliveryMode = _properties.RepublishDeliveryMode.Value;
            }

            _template.Send(_exchange, _routingKey ?? accessor.ConsumerQueue, errorMessage);

            if (_properties.AcknowledgeMode == AcknowledgeMode.Manual)
            {
                ulong deliveryTag = errorMessage.Headers.DeliveryTag().GetValueOrDefault();
                var channel = errorMessage.Headers[RabbitMessageHeaders.Channel] as IModel;
                channel.BasicAck(deliveryTag, false);
            }
        }

        private static bool ShouldRepublish(Exception exception)
        {
            Exception cause = exception;

            while (cause != null && cause is not RabbitRejectAndDoNotRequeueException && cause is not ImmediateAcknowledgeException)
            {
                cause = cause.InnerException;
            }

            return cause is not ImmediateAcknowledgeException;
        }

        private static string GetDeadLetterExchangeName(RabbitCommonOptions properties)
        {
            if (properties.DeadLetterExchange == null)
            {
                return ApplyPrefix(properties.Prefix, RabbitCommonOptions.DeadLetterExchangeName);
            }

            return properties.DeadLetterExchange;
        }
    }

    private sealed class RabbitBinderConsumerTagStrategy : IConsumerTagStrategy
    {
        private readonly string _prefix;

        private readonly AtomicInteger _index = new();

        public string ServiceName { get; set; } = nameof(RabbitBinderConsumerTagStrategy);

        public RabbitBinderConsumerTagStrategy(string prefix)
        {
            _prefix = prefix;
        }

        public string CreateConsumerTag(string queue)
        {
            return $"{_prefix}#{_index.GetAndIncrement()}";
        }
    }

    private sealed class DefaultBinderMessagePropertiesConverter : DefaultMessageHeadersConverter
    {
        public override IMessageHeaders ToMessageHeaders(IBasicProperties source, Envelope envelope, Encoding charset)
        {
            IMessageHeaders result = base.ToMessageHeaders(source, envelope, charset);
            RabbitHeaderAccessor accessor = RabbitHeaderAccessor.GetMutableAccessor(result);
            accessor.DeliveryMode = null;
            return accessor.MessageHeaders;
        }
    }

    private sealed class SimplePassThroughMessageConverter : AbstractMessageConverter
    {
        private readonly SimpleMessageConverter _converter = new();

        public override string ServiceName
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public override object FromMessage(IMessage message, Type targetType, object conversionHint)
        {
            return message.Payload;
        }

        protected override IMessage CreateMessage(object payload, IMessageHeaders headers, object conversionHint)
        {
            if (payload is byte[] payloadBytes)
            {
                return Message.Create(payloadBytes, headers);
            }

            // just for safety (backwards compatibility)
            return _converter.ToMessage(payload, headers);
        }
    }
}
