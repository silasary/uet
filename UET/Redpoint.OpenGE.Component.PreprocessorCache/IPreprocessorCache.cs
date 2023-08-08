﻿namespace Redpoint.OpenGE.Component.PreprocessorCache
{
    using Redpoint.OpenGE.Protocol;
    using System.Threading.Tasks;

    public interface IPreprocessorCache
    {
        Task EnsureAsync();

        Task<PreprocessorScanResultWithCacheMetadata> GetUnresolvedDependenciesAsync(
            string filePath,
            CancellationToken cancellationToken);

        Task<PreprocessorResolutionResultWithTimingMetadata> GetResolvedDependenciesAsync(
            string filePath,
            string[] forceIncludesFromPch,
            string[] forceIncludes,
            string[] includeDirectories,
            Dictionary<string, string> globalDefinitions,
            CancellationToken cancellationToken);
    }
}
