// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

namespace Steeltoe.Common.Expression.Internal.Spring.Common;

public class TemplateParserContext : IParserContext
{
    public bool IsTemplate { get; } = true;

    public string ExpressionPrefix { get; }

    public string ExpressionSuffix { get; }

    public TemplateParserContext()
        : this("#{", "}")
    {
    }

    public TemplateParserContext(string expressionPrefix, string expressionSuffix)
    {
        ExpressionPrefix = expressionPrefix;
        ExpressionSuffix = expressionSuffix;
    }
}
