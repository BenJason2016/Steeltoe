// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using Steeltoe.Common;
using Steeltoe.Common.HealthChecks;
using Steeltoe.Common.Util;
using Steeltoe.Connectors.MongoDb.DynamicTypeAccess;

namespace Steeltoe.Connectors.MongoDb;

internal sealed class MongoDbHealthContributor : IHealthContributor
{
    private readonly MongoClientInterfaceShim _mongoClientShim;
    private readonly ILogger<MongoDbHealthContributor> _logger;

    public string Id { get; } = "MongoDB";
    public string Host { get; }
    public string? ServiceName { get; set; }

    public MongoDbHealthContributor(object mongoClient, string host, ILogger<MongoDbHealthContributor> logger)
    {
        ArgumentGuard.NotNull(logger);
        ArgumentGuard.NotNullOrEmpty(host);
        ArgumentGuard.NotNull(mongoClient);

        _mongoClientShim = new MongoClientInterfaceShim(MongoDbPackageResolver.Default, mongoClient);
        Host = host;
        _logger = logger;
    }

    public async Task<HealthCheckResult?> HealthAsync(CancellationToken cancellationToken)
    {
        _logger.LogTrace("Checking {DbConnection} health at {Host}", Id, Host);

        var result = new HealthCheckResult
        {
            Details =
            {
                ["host"] = Host
            }
        };

        if (!string.IsNullOrEmpty(ServiceName))
        {
            result.Details["service"] = ServiceName;
        }

        try
        {
            using IDisposable cursor = await _mongoClientShim.ListDatabaseNamesAsync(cancellationToken);

            result.Status = HealthStatus.Up;
            result.Details.Add("status", HealthStatus.Up.ToSnakeCaseString(SnakeCaseStyle.AllCaps));

            _logger.LogTrace("{DbConnection} at {Host} is up!", Id, Host);
        }
        catch (Exception exception)
        {
            exception = exception.UnwrapAll();

            if (exception is OperationCanceledException)
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
            }

            _logger.LogError(exception, "{DbConnection} at {Host} is down!", Id, Host);

            result.Status = HealthStatus.Down;
            result.Description = $"{Id} health check failed";
            result.Details.Add("error", $"{exception.GetType().Name}: {exception.Message}");
            result.Details.Add("status", HealthStatus.Down.ToSnakeCaseString(SnakeCaseStyle.AllCaps));
        }

        return result;
    }
}
