// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

namespace Steeltoe.Common.Expression.Internal.Spring;

public class DefaultParseContext : IParserContext
{
    public static readonly IParserContext TemplateExpression = new DefaultParseContext();

    public bool IsTemplate => true;

    public string ExpressionPrefix => "#{";

    public string ExpressionSuffix => "}";
}
