// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Text;

namespace Steeltoe.Stream.Binder.Test;

public static class TestExtensions
{
    public static byte[] GetBytes(this string input)
    {
        return Encoding.UTF8.GetBytes(input);
    }

    public static string GetString(this byte[] input)
    {
        return Encoding.UTF8.GetString(input);
    }
}
