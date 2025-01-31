// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using System.Reflection.Emit;
using Steeltoe.Common.Expression.Internal.Spring.Support;
using Steeltoe.Common.Util;

namespace Steeltoe.Common.Expression.Internal.Spring.Ast;

public class FunctionReference : SpelNode
{
    private readonly string _name;

    // Captures the most recently used method for the function invocation *if* the method
    // can safely be used for compilation (i.e. no argument conversion is going on)
    private volatile MethodInfo _method;

    public FunctionReference(string functionName, int startPos, int endPos, params SpelNode[] arguments)
        : base(startPos, endPos, arguments)
    {
        _name = functionName;
    }

    public override ITypedValue GetValueInternal(ExpressionState state)
    {
        ITypedValue value = state.LookupVariable(_name);

        if (Equals(value, TypedValue.Null))
        {
            throw new SpelEvaluationException(StartPosition, SpelMessage.FunctionNotDefined, _name);
        }

        if (value.Value is not MethodInfo method)
        {
            // Possibly a static method registered as a function
            throw new SpelEvaluationException(SpelMessage.FunctionReferenceCannotBeInvoked, _name, value.GetType());
        }

        try
        {
            return ExecuteFunctionJlrMethod(state, method);
        }
        catch (SpelEvaluationException ex)
        {
            ex.Position = StartPosition;
            throw;
        }
    }

    public override string ToStringAst()
    {
        var items = new List<string>();

        for (int i = 0; i < ChildCount; i++)
        {
            items.Add(GetChild(i).ToStringAst());
        }

        return $"#{_name}({string.Join(",", items)})";
    }

    public override bool IsCompilable()
    {
        if (_method == null)
        {
            return false;
        }

        if (!_method.IsStatic || !_method.IsPublic || !ReflectionHelper.IsPublic(_method.DeclaringType))
        {
            return false;
        }

        foreach (SpelNode child in children)
        {
            if (!child.IsCompilable())
            {
                return false;
            }
        }

        return true;
    }

    public override void GenerateCode(ILGenerator gen, CodeFlow cf)
    {
        MethodInfo method = _method;

        if (method == null)
        {
            throw new InvalidOperationException("No method handle");
        }

        GenerateCodeForArguments(gen, cf, method, children);
        gen.Emit(OpCodes.Call, method);
        cf.PushDescriptor(exitTypeDescriptor);
    }

    private object[] GetArguments(ExpressionState state)
    {
        // Compute arguments to the function
        object[] arguments = new object[ChildCount];

        for (int i = 0; i < arguments.Length; i++)
        {
            arguments[i] = children[i].GetValueInternal(state).Value;
        }

        return arguments;
    }

    private TypedValue ExecuteFunctionJlrMethod(ExpressionState state, MethodInfo method)
    {
        object[] functionArgs = GetArguments(state);

        if (!method.IsVarArgs())
        {
            int declaredParamCount = method.GetParameters().Length;

            if (declaredParamCount != functionArgs.Length)
            {
                throw new SpelEvaluationException(SpelMessage.IncorrectNumberOfArgumentsToFunction, functionArgs.Length, declaredParamCount);
            }
        }

        if (!method.IsStatic)
        {
            throw new SpelEvaluationException(StartPosition, SpelMessage.FunctionMustBeStatic, ClassUtils.GetQualifiedMethodName(method), _name);
        }

        // Convert arguments if necessary and remap them for varargs if required
        ITypeConverter converter = state.EvaluationContext.TypeConverter;
        bool argumentConversionOccurred = ReflectionHelper.ConvertAllArguments(converter, functionArgs, method);

        if (method.IsVarArgs())
        {
            functionArgs = ReflectionHelper.SetupArgumentsForVarargsInvocation(ClassUtils.GetParameterTypes(method), functionArgs);
        }

        bool compilable = false;

        try
        {
            object result = method.Invoke(method.GetType(), functionArgs);
            compilable = !argumentConversionOccurred;
            return new TypedValue(result, result?.GetType() ?? method.ReturnType);
        }
        catch (Exception ex)
        {
            throw new SpelEvaluationException(StartPosition, ex, SpelMessage.ExceptionDuringFunctionCall, _name, ex.Message);
        }
        finally
        {
            if (compilable)
            {
                exitTypeDescriptor = CodeFlow.ToDescriptor(method.ReturnType);
                _method = method;
            }
            else
            {
                exitTypeDescriptor = null;
                _method = null;
            }
        }
    }
}
