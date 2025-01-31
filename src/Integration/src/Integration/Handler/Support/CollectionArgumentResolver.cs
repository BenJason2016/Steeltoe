// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Reflection;
using Steeltoe.Common.Contexts;
using Steeltoe.Integration.Util;
using Steeltoe.Messaging;
using Steeltoe.Messaging.Handler.Invocation;

namespace Steeltoe.Integration.Handler.Support;

public class CollectionArgumentResolver : AbstractExpressionEvaluator, IHandlerMethodArgumentResolver
{
    private readonly bool _canProcessMessageList;

    public CollectionArgumentResolver(IApplicationContext context, bool canProcessMessageList)
        : base(context)
    {
        _canProcessMessageList = canProcessMessageList;
    }

    public bool SupportsParameter(ParameterInfo parameter)
    {
        Type parameterType = parameter.ParameterType;
        return typeof(ICollection).IsAssignableFrom(parameterType) || typeof(IEnumerator).IsAssignableFrom(parameterType) || parameterType.IsSZArray;
    }

    public object ResolveArgument(ParameterInfo parameter, IMessage message)
    {
        object value = message.Payload;

        if (_canProcessMessageList)
        {
            if (value is not ICollection<IMessage> messages)
            {
                throw new InvalidOperationException(
                    $"This Argument Resolver only supports messages with a payload of ICollection<IMessage>, payload is: {value.GetType()}");
            }

            Type paramType = parameter.ParameterType;

            if (paramType.IsGenericType && typeof(IMessage).IsAssignableFrom(paramType.GetGenericArguments()[0]))
            {
                value = messages;
            }
            else
            {
                var payloadList = new List<object>();

                foreach (IMessage m in messages)
                {
                    payloadList.Add(m.Payload);
                }

                value = payloadList;
            }
        }

        if (typeof(IEnumerator).IsAssignableFrom(parameter.ParameterType))
        {
            if (value is IEnumerator)
            {
                return value;
            }

            return new List<object>
            {
                value
            }.GetEnumerator();
        }

        return EvaluationContext.TypeConverter.ConvertValue(value, value?.GetType(), parameter.ParameterType);
    }
}
