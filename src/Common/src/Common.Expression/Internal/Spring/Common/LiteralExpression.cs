// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

namespace Steeltoe.Common.Expression.Internal.Spring.Common;

public class LiteralExpression : IExpression
{
    public string ExpressionString { get; }

    public LiteralExpression(string literalValue)
    {
        ExpressionString = literalValue;
    }

    public string GetValue()
    {
        return ExpressionString;
    }

    public T GetValue<T>()
    {
        return (T)GetValue(typeof(T));
    }

    public object GetValue(Type desiredResultType)
    {
        object value = GetValue();
        return ExpressionUtils.ConvertTypedValue(null, new TypedValue(value), desiredResultType);
    }

    public string GetValue(object rootObject)
    {
        return ExpressionString;
    }

    public T GetValue<T>(object rootObject)
    {
        return (T)GetValue(rootObject, typeof(T));
    }

    public object GetValue(object rootObject, Type desiredResultType)
    {
        object value = GetValue(rootObject);
        return ExpressionUtils.ConvertTypedValue(null, new TypedValue(value), desiredResultType);
    }

    public string GetValue(IEvaluationContext context)
    {
        return ExpressionString;
    }

    public object GetValue(IEvaluationContext context, Type desiredResultType)
    {
        object value = GetValue(context);
        return ExpressionUtils.ConvertTypedValue(context, new TypedValue(value), desiredResultType);
    }

    public T GetValue<T>(IEvaluationContext context)
    {
        return (T)GetValue(context, typeof(T));
    }

    public string GetValue(IEvaluationContext context, object rootObject)
    {
        return ExpressionString;
    }

    public T GetValue<T>(IEvaluationContext context, object rootObject)
    {
        return (T)GetValue(context, rootObject, typeof(T));
    }

    public object GetValue(IEvaluationContext context, object rootObject, Type desiredResultType)
    {
        object value = GetValue(context, rootObject);
        return ExpressionUtils.ConvertTypedValue(context, new TypedValue(value), desiredResultType);
    }

    public Type GetValueType()
    {
        return typeof(string);
    }

    public Type GetValueType(object rootObject)
    {
        return typeof(string);
    }

    public Type GetValueType(IEvaluationContext context, object rootObject)
    {
        return typeof(string);
    }

    public Type GetValueType(IEvaluationContext context)
    {
        return typeof(string);
    }

    public bool IsWritable(object rootObject)
    {
        return false;
    }

    public bool IsWritable(IEvaluationContext context)
    {
        return false;
    }

    public bool IsWritable(IEvaluationContext context, object rootObject)
    {
        return false;
    }

    public void SetValue(object rootObject, object value)
    {
        throw new EvaluationException(ExpressionString, "Cannot call SetValue() on a LiteralExpression");
    }

    public void SetValue(IEvaluationContext context, object value)
    {
        throw new EvaluationException(ExpressionString, "Cannot call SetValue() on a LiteralExpression");
    }

    public void SetValue(IEvaluationContext context, object rootObject, object value)
    {
        throw new EvaluationException(ExpressionString, "Cannot call SetValue() on a LiteralExpression");
    }

    object IExpression.GetValue()
    {
        return GetValue();
    }

    object IExpression.GetValue(object rootObject)
    {
        return GetValue(rootObject);
    }

    object IExpression.GetValue(IEvaluationContext context)
    {
        return GetValue(context);
    }

    object IExpression.GetValue(IEvaluationContext context, object rootObject)
    {
        return GetValue(context, rootObject);
    }
}
