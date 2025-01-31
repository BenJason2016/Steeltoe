// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using Steeltoe.Common.Expression.Internal;
using Steeltoe.Common.Expression.Internal.Spring;
using Steeltoe.Common.Expression.Internal.Spring.Standard;
using Xunit;

namespace Steeltoe.Common.Expression.Test.Spring;

public sealed class ArrayConstructorTests : AbstractExpressionTests
{
    [Fact]
    public void SimpleArrayWithInitializer()
    {
        EvaluateArrayBuildingExpression("new int[]{1,2,3}", "[1,2,3]");
        EvaluateArrayBuildingExpression("new int[]{}", "[]");
        Evaluate("new int[]{}.Length", "0", typeof(int));
        Evaluate("new int[]{1,2,3}.Length", "3", typeof(int));
    }

    [Fact]
    public void Conversion()
    {
        Evaluate("new String[]{1,2,3}[0]", "1", typeof(string));
        Evaluate("new int[]{'123'}[0]", 123, typeof(int));
    }

    [Fact]
    public void MultidimensionalArrays()
    {
        EvaluateAndCheckError("new int[][]{{1,2},{3,4}}", SpelMessage.MultidimensionalArrayInitializerNotSupported);
        EvaluateAndCheckError("new int[3][]", SpelMessage.MissingArrayDimension);
        EvaluateAndCheckError("new int[]", SpelMessage.MissingArrayDimension);
        EvaluateAndCheckError("new String[]", SpelMessage.MissingArrayDimension);
        EvaluateAndCheckError("new int[][1]", SpelMessage.MissingArrayDimension);
    }

    [Fact]
    public void PrimitiveTypeArrayConstructors()
    {
        EvaluateArrayBuildingExpression("new int[]{1,2,3,4}", "[1,2,3,4]");
        EvaluateArrayBuildingExpression("new boolean[]{true,false,true}", "[True,False,True]");
        EvaluateArrayBuildingExpression("new char[]{'a','b','c'}", "[a,b,c]");
        EvaluateArrayBuildingExpression("new long[]{1,2,3,4,5}", "[1,2,3,4,5]");
        EvaluateArrayBuildingExpression("new short[]{2,3,4,5,6}", "[2,3,4,5,6]");
        EvaluateArrayBuildingExpression("new double[]{1d,2d,3d,4d}", "[1.0,2.0,3.0,4.0]");
        EvaluateArrayBuildingExpression("new float[]{1f,2f,3f,4f}", "[1.0,2.0,3.0,4.0]");
        EvaluateArrayBuildingExpression("new byte[]{1,2,3,4}", "[1,2,3,4]");
    }

    [Fact]
    public void PrimitiveTypeArrayConstructorsElements()
    {
        Evaluate("new int[]{1,2,3,4}[0]", 1, typeof(int));
        Evaluate("new boolean[]{true,false,true}[0]", true, typeof(bool));
        Evaluate("new char[]{'a','b','c'}[0]", 'a', typeof(char));
        Evaluate("new long[]{1,2,3,4,5}[0]", 1L, typeof(long));
        Evaluate("new short[]{2,3,4,5,6}[0]", (short)2, typeof(short));
        Evaluate("new double[]{1d,2d,3d,4d}[0]", 1D, typeof(double));
        Evaluate("new float[]{1f,2f,3f,4f}[0]", 1F, typeof(float));
        Evaluate("new byte[]{1,2,3,4}[0]", (byte)1, typeof(byte));
        Evaluate("new String(new char[]{'h','e','l','l','o'})", "hello", typeof(string));
    }

    [Fact]
    public void ErrorCases()
    {
        EvaluateAndCheckError("new char[7]{'a','c','d','e'}", SpelMessage.InitializerLengthIncorrect);
        EvaluateAndCheckError("new char[3]{'a','c','d','e'}", SpelMessage.InitializerLengthIncorrect);
        EvaluateAndCheckError("new char[2]{'hello','world'}", SpelMessage.TypeConversionError);
        EvaluateAndCheckError("new String('a','c','d')", SpelMessage.ConstructorInvocationProblem);
    }

    [Fact]
    public void TypeArrayConstructors()
    {
        Evaluate("new String[]{'a','b','c','d'}[1]", "b", typeof(string));
        EvaluateAndCheckError("new String[]{'a','b','c','d'}.size()", SpelMessage.MethodNotFound, 30, "size()", "System.String[]");
        Evaluate("new String[]{'a','b','c','d'}.Length", 4, typeof(int));
    }

    [Fact]
    public void BasicArray()
    {
        Evaluate("new String[3]", "System.String[3]{(0)=null,(1)=null,(2)=null,}", typeof(string[]));
    }

    [Fact]
    public void MultiDimensionalArray()
    {
        Evaluate("new String[2][2]", "System.String[2][2]{(0,0)=null,(0,1)=null,(1,0)=null,(1,1)=null,}", typeof(string[,]));

        Evaluate("new String[3][2][1]", "System.String[3][2][1]{(0,0,0)=null,(0,1,0)=null,(1,0,0)=null,(1,1,0)=null,(2,0,0)=null,(2,1,0)=null,}",
            typeof(string[,,]));
    }

    [Fact]
    public void ConstructorInvocation03()
    {
        EvaluateAndCheckError("new String[]", SpelMessage.MissingArrayDimension);
    }

    [Fact]
    public void ConstructorInvocation04()
    {
        EvaluateAndCheckError("new int[3]{'3','ghi','5'}", SpelMessage.TypeConversionError, 0);
    }

    private void EvaluateArrayBuildingExpression(string expression, string expectedToString)
    {
        var parser = new SpelExpressionParser();
        IExpression e = parser.ParseExpression(expression);
        object o = e.GetValue();
        Assert.NotNull(o);
        Assert.True(o.GetType().IsArray);
        var s = new StringBuilder();
        s.Append('[');

        if (o is int[] iArray)
        {
            for (int i = 0; i < iArray.Length; i++)
            {
                if (i > 0)
                {
                    s.Append(',');
                }

                s.Append(iArray[i]);
            }
        }
        else if (o is bool[] bArray)
        {
            for (int i = 0; i < bArray.Length; i++)
            {
                if (i > 0)
                {
                    s.Append(',');
                }

                s.Append(bArray[i]);
            }
        }
        else if (o is char[] cArray)
        {
            for (int i = 0; i < cArray.Length; i++)
            {
                if (i > 0)
                {
                    s.Append(',');
                }

                s.Append(cArray[i]);
            }
        }
        else if (o is long[] lArray)
        {
            for (int i = 0; i < lArray.Length; i++)
            {
                if (i > 0)
                {
                    s.Append(',');
                }

                s.Append(lArray[i]);
            }
        }
        else if (o is short[] sArray)
        {
            for (int i = 0; i < sArray.Length; i++)
            {
                if (i > 0)
                {
                    s.Append(',');
                }

                s.Append(sArray[i]);
            }
        }
        else if (o is double[] dArray)
        {
            for (int i = 0; i < dArray.Length; i++)
            {
                if (i > 0)
                {
                    s.Append(',');
                }

                s.Append(dArray[i].ToString("F1", CultureInfo.InvariantCulture));
            }
        }
        else if (o is float[] fArray)
        {
            for (int i = 0; i < fArray.Length; i++)
            {
                if (i > 0)
                {
                    s.Append(',');
                }

                s.Append(fArray[i].ToString("F1", CultureInfo.InvariantCulture));
            }
        }
        else if (o is byte[] byArray)
        {
            for (int i = 0; i < byArray.Length; i++)
            {
                if (i > 0)
                {
                    s.Append(',');
                }

                s.Append(byArray[i]);
            }
        }
        else
        {
            throw new InvalidOperationException($"Not supported {o.GetType()}");
        }

        s.Append(']');
        Assert.Equal(expectedToString, s.ToString());
    }
}
