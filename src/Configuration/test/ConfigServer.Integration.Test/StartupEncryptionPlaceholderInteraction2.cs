// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Steeltoe.Configuration.Encryption;
using Steeltoe.Configuration.Placeholder;

namespace Steeltoe.Configuration.ConfigServer.Integration.Test;

public sealed class StartupEncryptionPlaceholderInteraction2
{
    private readonly IConfiguration _configuration;

    internal static IServiceProvider ServiceProvider { get; private set; }

    public StartupEncryptionPlaceholderInteraction2(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        IConfiguration configuration = services.ConfigureEncryptionResolver(_configuration);
        services.ConfigurePlaceholderResolver(configuration);
    }

    public void Configure(IApplicationBuilder app)
    {
        ServiceProvider = app.ApplicationServices;
    }
}
