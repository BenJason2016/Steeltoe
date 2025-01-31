// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using Steeltoe.Common.Util;
using Steeltoe.Messaging.Converter;
using Steeltoe.Stream.Configuration;

namespace Steeltoe.Stream.Converter;

public class CompositeMessageConverterFactory : IMessageConverterFactory
{
    public ISmartMessageConverter MessageConverterForAllRegistered => new CompositeMessageConverter(new List<IMessageConverter>(AllRegistered));

    public IList<IMessageConverter> AllRegistered { get; }

    public CompositeMessageConverterFactory()
        : this(null)
    {
    }

    public CompositeMessageConverterFactory(IEnumerable<IMessageConverter> converters)
    {
        AllRegistered = converters == null ? new List<IMessageConverter>() : new List<IMessageConverter>(converters);

        InitDefaultConverters();

        var resolver = new DefaultContentTypeResolver
        {
            DefaultMimeType = BindingOptions.DefaultContentType
        };

        foreach (IMessageConverter mc in AllRegistered)
        {
            if (mc is AbstractMessageConverter converter)
            {
                converter.ContentTypeResolver = resolver;
            }
        }
    }

    public IMessageConverter GetMessageConverterForType(MimeType mimeType)
    {
        var converters = new List<IMessageConverter>();

        foreach (IMessageConverter converter in AllRegistered)
        {
            if (converter is AbstractMessageConverter abstractMessageConverter)
            {
                foreach (MimeType type in abstractMessageConverter.SupportedMimeTypes)
                {
                    if (type.Includes(mimeType))
                    {
                        converters.Add(converter);
                    }
                }
            }
        }

        return converters.Count switch
        {
            0 => throw new ConversionException($"No message converter is registered for {mimeType}"),
            > 1 => new CompositeMessageConverter(converters),
            _ => converters[0]
        };
    }

    private void InitDefaultConverters()
    {
        var applicationJsonConverter = new ApplicationJsonMessageMarshallingConverter();

        AllRegistered.Add(applicationJsonConverter);

        AllRegistered.Add(new ObjectSupportingByteArrayMessageConverter());
        AllRegistered.Add(new ObjectStringMessageConverter());
    }
}
