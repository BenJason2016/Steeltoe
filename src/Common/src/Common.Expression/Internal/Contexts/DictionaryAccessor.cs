// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using Steeltoe.Common.Expression.Internal.Spring;

namespace Steeltoe.Common.Expression.Internal.Contexts;

public class DictionaryAccessor : ICompilablePropertyAccessor
{
    private static readonly MethodInfo GetItem = typeof(IDictionary).GetMethod("get_Item", new[]
    {
        typeof(object)
    });

    public bool CanRead(IEvaluationContext context, object target, string name)
    {
        return target is IDictionary dictionary && dictionary.Contains(name);
    }

    public bool CanWrite(IEvaluationContext context, object target, string name)
    {
        return true;
    }

    public Type GetPropertyType()
    {
        return typeof(object);
    }

    public IList<Type> GetSpecificTargetClasses()
    {
        return new List<Type>
        {
            typeof(IDictionary)
        };
    }

    public bool IsCompilable()
    {
        return true;
    }

    public ITypedValue Read(IEvaluationContext context, object target, string name)
    {
        if (target is not IDictionary dictionary)
        {
            throw new ArgumentException($"Target must be of type {nameof(IDictionary)}", nameof(target));
        }

        if (dictionary.Contains(name))
        {
            return new TypedValue(dictionary[name]);
        }

        throw new DictionaryAccessException(name);
    }

    public void Write(IEvaluationContext context, object target, string name, object newValue)
    {
        if (target is not IDictionary dictionary)
        {
            throw new ArgumentException($"Target must be of type {nameof(IDictionary)}", nameof(target));
        }

        dictionary[name] = newValue;
    }

    public void GenerateCode(string propertyName, ILGenerator gen, CodeFlow cf)
    {
        TypeDescriptor descriptor = cf.LastDescriptor();

        if (descriptor == null || descriptor.Value != typeof(IDictionary))
        {
            if (descriptor == null)
            {
                CodeFlow.LoadTarget(gen);
            }

            gen.Emit(OpCodes.Castclass, typeof(IDictionary));
        }

        gen.Emit(OpCodes.Ldstr, propertyName);
        gen.Emit(OpCodes.Callvirt, GetItem);
    }

    private sealed class DictionaryAccessException : AccessException
    {
        private readonly string _key;

        public override string Message => $"Dictionary does not contain a value for key '{_key}'";

        public DictionaryAccessException(string key)
            : base(string.Empty)
        {
            _key = key;
        }
    }
}
