// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using Steeltoe.Messaging;

namespace Steeltoe.Integration;

public class MessageTimeoutException : MessageDeliveryException
{
    public MessageTimeoutException(string message)
        : base(message)
    {
    }

    public MessageTimeoutException(IMessage failedMessage)
        : base(failedMessage)
    {
    }

    public MessageTimeoutException(IMessage failedMessage, string message)
        : base(failedMessage, message)
    {
    }

    public MessageTimeoutException(IMessage failedMessage, string message, Exception innerException)
        : base(failedMessage, message, innerException)
    {
    }
}
