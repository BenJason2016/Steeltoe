// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using Steeltoe.Configuration.CloudFoundry;

namespace Steeltoe.Configuration.ConfigServer.Test;

internal sealed class CustomCloudFoundrySettingsReader : ICloudFoundrySettingsReader
{
    public string ApplicationJson => throw new NotImplementedException();

    public string InstanceId => throw new NotImplementedException();

    public string InstanceIndex => throw new NotImplementedException();

    public string InstanceInternalIP => throw new NotImplementedException();

    public string InstanceIP => throw new NotImplementedException();

    public string InstancePort => throw new NotImplementedException();

    public string ServicesJson => throw new NotImplementedException();
}
