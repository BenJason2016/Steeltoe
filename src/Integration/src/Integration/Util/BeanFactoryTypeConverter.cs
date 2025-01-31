// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using Steeltoe.Common;
using Steeltoe.Common.Converter;
using Steeltoe.Common.Expression.Internal;

namespace Steeltoe.Integration.Util;

public class BeanFactoryTypeConverter : ITypeConverter
{
    public IConversionService ConversionService { get; set; }

    public BeanFactoryTypeConverter()
    {
        ConversionService = DefaultConversionService.Singleton;
    }

    public BeanFactoryTypeConverter(IConversionService conversionService)
    {
        ArgumentGuard.NotNull(conversionService);

        ConversionService = conversionService;
    }

    public bool CanConvert(Type sourceType, Type targetType)
    {
        return ConversionService.CanConvert(sourceType, targetType);
    }

    public object ConvertValue(object value, Type sourceType, Type targetType)
    {
        if (CanConvert(sourceType, targetType))
        {
            return ConversionService.Convert(value, sourceType, targetType);
        }

        throw new NotImplementedException("Cannot be converted by default service");
    }
}
