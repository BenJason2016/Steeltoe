// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

namespace Steeltoe.Common.Converter;

public class ArrayToArrayConverter : AbstractGenericConditionalConverter
{
    private readonly IConversionService _conversionService;

    public ArrayToArrayConverter(IConversionService conversionService)
        : base(new HashSet<(Type SourceType, Type TargetType)>
        {
            (typeof(object[]), typeof(object[]))
        })
    {
        _conversionService = conversionService;
    }

    public override bool Matches(Type sourceType, Type targetType)
    {
        if (!sourceType.IsArray || !targetType.IsArray)
        {
            return false;
        }

        return ConversionUtils.CanConvertElements(ConversionUtils.GetElementType(sourceType), ConversionUtils.GetElementType(targetType), _conversionService);
    }

    public override object Convert(object source, Type sourceType, Type targetType)
    {
        Type targetElement = ConversionUtils.GetElementType(targetType);
        Type sourceElement = ConversionUtils.GetElementType(sourceType);

        if (targetElement != null && _conversionService.CanBypassConvert(sourceElement, targetElement))
        {
            return source;
        }

        var sourceArray = source as Array;
        var targetArray = Array.CreateInstance(targetElement, sourceArray.GetLength(0));
        int i = 0;

        foreach (object elem in sourceArray)
        {
            object newElem = _conversionService.Convert(elem, sourceElement, targetElement);
            targetArray.SetValue(newElem, i++);
        }

        return targetArray;
    }
}
