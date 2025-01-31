// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using Steeltoe.Common.Converter;

namespace Steeltoe.Common.Expression.Test.Spring;

public sealed class TestGenericConversionService : IConversionService
{
    public bool CanBypassConvert(Type sourceType, Type targetType)
    {
        return false;
    }

    public bool CanConvert(Type sourceType, Type targetType)
    {
        return true;
    }

    public T Convert<T>(object source)
    {
        return (T)Convert(source, source?.GetType(), typeof(T));
    }

    public object Convert(object source, Type sourceType, Type targetType)
    {
        if (source == null)
        {
            return targetType == typeof(bool) ? false : null;
        }

        return source;
    }
}
