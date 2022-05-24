﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.DependencyInjection;
using System;
using Xunit;

namespace Steeltoe.Management.Tracing.Test
{
    public class TracingCoreHostBuilderExtensionsTest : TestBase
    {
        [Fact]
        public void AddDistributedTracingAspNetCore_ConfiguresExpectedDefaults()
        {
#if !NET6_0
            AppContext.SetSwitch(
                "System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
#endif
            var services = new ServiceCollection().AddSingleton(GetConfiguration());

            var serviceProvider = services.AddDistributedTracingAspNetCore().BuildServiceProvider();

            ValidateServiceCollectionCommon(serviceProvider);
            ValidateServiceContainerCore(serviceProvider);
        }
    }
}
