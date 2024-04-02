﻿namespace Redpoint.Uet.BuildPipeline.Providers.Test
{
    using Redpoint.RuntimeJson;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.Automation;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.Commandlet;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.Custom;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.Downstream;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.Gauntlet;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.ProjectPackage;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Project.Automation;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Project.Commandlet;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Project.Custom;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Project.Gauntlet;
    using System.Text.Json.Serialization;

    [RuntimeJsonProvider(typeof(TestProviderSourceGenerationContext))]
    [JsonSerializable(typeof(BuildConfigPluginTestAutomation))]
    [JsonSerializable(typeof(BuildConfigPluginTestCommandlet))]
    [JsonSerializable(typeof(BuildConfigPluginTestCustom))]
    [JsonSerializable(typeof(BuildConfigPluginTestGauntlet))]
    [JsonSerializable(typeof(BuildConfigPluginTestProjectPackage))]
    [JsonSerializable(typeof(BuildConfigPluginTestDownstream))]
    [JsonSerializable(typeof(BuildConfigProjectTestAutomation))]
    [JsonSerializable(typeof(BuildConfigProjectTestCustom))]
    [JsonSerializable(typeof(BuildConfigProjectTestGauntlet))]
    [JsonSerializable(typeof(BuildConfigProjectTestCommandlet))]
    internal sealed partial class TestProviderRuntimeJson
    {
    }
}
