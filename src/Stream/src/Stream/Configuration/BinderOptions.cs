// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

namespace Steeltoe.Stream.Configuration;

public class BinderOptions : IBinderOptions
{
    private const bool InheritEnvironmentDefault = true;
    private const bool DefaultCandidateDefault = true;

    bool IBinderOptions.InheritEnvironment => InheritEnvironment.Value;

    bool IBinderOptions.DefaultCandidate => DefaultCandidate.Value;

    public string ConfigureClass { get; set; }

    public string ConfigureAssembly { get; set; }

    public Dictionary<string, object> Environment { get; set; }

    public bool? InheritEnvironment { get; set; }

    public bool? DefaultCandidate { get; set; }

    internal void PostProcess()
    {
        Environment ??= new Dictionary<string, object>();
        InheritEnvironment ??= InheritEnvironmentDefault;
        DefaultCandidate ??= DefaultCandidateDefault;
    }
}
