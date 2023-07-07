﻿namespace Redpoint.Uet.BuildPipeline.Providers.Prepare.Plugin.Custom
{
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.BuildGraph;
    using Redpoint.Uet.BuildPipeline.Providers.Prepare.Project;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading.Tasks;
    using System.Xml;

    internal class CustomPluginPrepareProvider : IPluginPrepareProvider
    {
        private readonly IScriptExecutor _scriptExecutor;

        public CustomPluginPrepareProvider(
            IScriptExecutor scriptExecutor)
        {
            _scriptExecutor = scriptExecutor;
        }

        public string Type => "Custom";

        public JsonTypeInfo DynamicSettingsJsonTypeInfo => PrepareProviderSourceGenerationContext.WithStringEnum.BuildConfigPluginPrepareCustom;

        public JsonSerializerContext DynamicSettingsJsonTypeInfoResolver => PrepareProviderSourceGenerationContext.WithStringEnum;

        public object DeserializeDynamicSettings(
            ref Utf8JsonReader reader,
            JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize(ref reader, PrepareProviderSourceGenerationContext.WithStringEnum.BuildConfigPluginPrepareCustom)!;
        }

        public async Task WriteBuildGraphNodesAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            BuildConfigPluginDistribution buildConfigDistribution,
            IEnumerable<BuildConfigDynamic<BuildConfigPluginDistribution, IPrepareProvider>> entries)
        {
            var castedSettings = entries
                .Select(x => (name: x.Name, settings: (BuildConfigPluginPrepareCustom)x.DynamicSettings))
                .ToList();

            foreach (var entry in castedSettings)
            {
                foreach (var runBefore in entry.settings.RunBefore ?? Array.Empty<BuildConfigPluginPrepareRunBefore>())
                {
                    switch (runBefore)
                    {
                        case BuildConfigPluginPrepareRunBefore.AssembleFinalize:
                            await writer.WriteMacroAsync(
                                new MacroElementProperties
                                {
                                    Name = $"CustomOnAssembleFinalize-{entry.name}",
                                    Arguments = Array.Empty<string>(),
                                },
                                async writer =>
                                {
                                    await writer.WriteSpawnAsync(
                                        new SpawnElementProperties
                                        {
                                            Exe = "powershell.exe",
                                            Arguments = new[]
                                            {
                                                "-ExecutionPolicy",
                                                "Bypass",
                                                $@"""$(ProjectRoot)/{entry.settings.ScriptPath}"""
                                            }
                                        });
                                });
                            await writer.WritePropertyAsync(
                                new PropertyElementProperties
                                {
                                    Name = "DynamicBeforeAssembleFinalizeMacros",
                                    Value = $"$(DynamicBeforeAssembleFinalizeMacros)CustomOnAssembleFinalize-{entry.name};",
                                });
                            break;
                        case BuildConfigPluginPrepareRunBefore.Compile:
                            await writer.WriteMacroAsync(
                                new MacroElementProperties
                                {
                                    Name = $"CustomOnCompile-{entry.name}",
                                    Arguments = new[]
                                    {
                                        "TargetType",
                                        "TargetName",
                                        "TargetPlatform",
                                        "TargetConfiguration",
                                        "HostPlatform",
                                    }
                                },
                                async writer =>
                                {
                                    await writer.WriteSpawnAsync(
                                        new SpawnElementProperties
                                        {
                                            Exe = "powershell.exe",
                                            Arguments = new[]
                                            {
                                                "-ExecutionPolicy",
                                                "Bypass",
                                                $@"""$(ProjectRoot)/{entry.settings.ScriptPath}""",
                                                "-TargetType",
                                                @"""$(TargetType)""",
                                                "-TargetName",
                                                @"""$(TargetName)""",
                                                "-TargetPlatform",
                                                @"""$(TargetPlatform)""",
                                                "-TargetConfiguration",
                                                @"""$(TargetConfiguration)""",
                                            },
                                            If = "$(HostPlatform) == 'Win64'"
                                        });
                                    await writer.WriteSpawnAsync(
                                        new SpawnElementProperties
                                        {
                                            Exe = "pwsh",
                                            Arguments = new[]
                                            {
                                                "-ExecutionPolicy",
                                                "Bypass",
                                                $@"""$(ProjectRoot)/{entry.settings.ScriptPath}""",
                                                "-TargetType",
                                                @"""$(TargetType)""",
                                                "-TargetName",
                                                @"""$(TargetName)""",
                                                "-TargetPlatform",
                                                @"""$(TargetPlatform)""",
                                                "-TargetConfiguration",
                                                @"""$(TargetConfiguration)""",
                                            },
                                            If = "$(HostPlatform) == 'Mac'"
                                        });
                                });
                            await writer.WritePropertyAsync(
                                new PropertyElementProperties
                                {
                                    Name = "DynamicBeforeCompileMacros",
                                    Value = $"$(DynamicBeforeCompileMacros)CustomOnCompile-{entry.name};",
                                });
                            break;
                        case BuildConfigPluginPrepareRunBefore.BuildGraph:
                            // We don't emit anything in the graph for these.
                            break;
                        default:
                            throw new NotSupportedException();
                    }
                }
            }
        }

        public async Task RunBeforeBuildGraphAsync(
            BuildConfigPluginDistribution buildConfigDistribution,
            IEnumerable<BuildConfigDynamic<BuildConfigPluginDistribution, IPrepareProvider>> entries,
            string repositoryRoot, CancellationToken cancellationToken)
        {
            var castedSettings = entries
                .Select(x => (name: x.Name, settings: (BuildConfigPluginPrepareCustom)x.DynamicSettings))
                .ToList();

            foreach (var entry in castedSettings
                .Where(x => (x.settings.RunBefore ?? Array.Empty<BuildConfigPluginPrepareRunBefore>()).Contains(BuildConfigPluginPrepareRunBefore.BuildGraph)))
            {
                await _scriptExecutor.ExecutePowerShellAsync(
                    new ScriptSpecification
                    {
                        ScriptPath = entry.settings.ScriptPath,
                        Arguments = Array.Empty<string>(),
                        WorkingDirectory = repositoryRoot,
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken);
            }
        }
    }
}
