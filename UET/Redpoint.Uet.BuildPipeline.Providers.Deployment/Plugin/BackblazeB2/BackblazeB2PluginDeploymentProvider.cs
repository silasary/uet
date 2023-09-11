﻿namespace Redpoint.Uet.BuildPipeline.Providers.Deployment.Plugin.BackblazeB2
{
    using Redpoint.RuntimeJson;
    using Redpoint.Uet.BuildGraph;
    using Redpoint.Uet.Configuration;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading.Tasks;
    using System.Xml;

    internal class BackblazeB2PluginDeploymentProvider : IPluginDeploymentProvider
    {
        private readonly IGlobalArgsProvider? _globalArgsProvider;

        public BackblazeB2PluginDeploymentProvider(
            IGlobalArgsProvider? globalArgsProvider = null)
        {
            _globalArgsProvider = globalArgsProvider;
        }

        public string Type => "BackblazeB2";

        public IRuntimeJson DynamicSettings { get; } = new DeploymentProviderRuntimeJson(DeploymentProviderSourceGenerationContext.WithStringEnum).BuildConfigPluginDeploymentBackblazeB2;

        public async Task WriteBuildGraphNodesAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            BuildConfigPluginDistribution buildConfigDistribution,
            IEnumerable<BuildConfigDynamic<BuildConfigPluginDistribution, IDeploymentProvider>> dynamicSettings)
        {
            var castedSettings = dynamicSettings
                .Select(x => (name: x.Name, manual: x.Manual ?? false, settings: (BuildConfigPluginDeploymentBackblazeB2)x.DynamicSettings))
                .ToList();

            // Emit the nodes to run each deployment.
            foreach (var deployment in castedSettings)
            {
                await writer.WriteAgentAsync(
                    new AgentElementProperties
                    {
                        Name = $"Deployment {deployment.name}",
                        Type = deployment.manual ? "Win64_Manual" : "Win64",
                    },
                    async writer =>
                    {
                        await writer.WriteNodeAsync(
                            new NodeElementProperties
                            {
                                Name = $"Deployment {deployment.name}",
                                Requires = "#PackagedZip;$(DynamicPreDeploymentNodes)",
                            },
                            async writer =>
                            {
                                await writer.WriteSpawnAsync(
                                    new SpawnElementProperties
                                    {
                                        Exe = "$(UETPath)",
                                        Arguments = (_globalArgsProvider?.GlobalArgsArray ?? Array.Empty<string>()).Concat(new[]
                                        {
                                            "internal",
                                            "upload-to-backblaze-b2",
                                            "--zip-path",
                                            $@"""$(ProjectRoot)/$(PluginName)-$(Distribution)-$(VersionName).zip""",
                                            "--bucket-name",
                                            $@"""{deployment.settings.BucketName}""",
                                            "--folder-env-var",
                                            $@"""{deployment.settings.FolderPrefixEnvVar}"""
                                        }).ToArray()
                                    });
                            });
                        await writer.WriteDynamicNodeAppendAsync(
                            new DynamicNodeAppendElementProperties
                            {
                                NodeName = $"Deployment {deployment.name}",
                            });
                    });
            }
        }
    }
}
