﻿namespace Redpoint.Uet.BuildPipeline.Executors.Engine
{
    using Microsoft.AspNetCore.Mvc.Formatters;
    using Redpoint.Reservation;
    using Redpoint.Uet.Workspace;
    using Redpoint.Uet.Workspace.Descriptors;
    using Redpoint.Uet.Workspace.Reservation;
    using System.Threading.Tasks;

    internal class DefaultEngineWorkspaceProvider : IEngineWorkspaceProvider
    {
        private readonly IDynamicWorkspaceProvider _workspaceProvider;
        private readonly IReservationManagerForUet _reservationManagerForUet;

        public DefaultEngineWorkspaceProvider(
            IDynamicWorkspaceProvider workspaceProvider,
            IReservationManagerForUet reservationManagerForUet)
        {
            _workspaceProvider = workspaceProvider;
            _reservationManagerForUet = reservationManagerForUet;
        }

        public async Task<IWorkspace> GetEngineWorkspace(
            BuildEngineSpecification buildEngineSpecification,
            string workspaceSuffix,
            CancellationToken cancellationToken)
        {
            if (buildEngineSpecification._enginePath != null)
            {
                if (_workspaceProvider.ProvidesFastCopyOnWrite)
                {
                    return await _workspaceProvider.GetWorkspaceAsync(
                        new FolderSnapshotWorkspaceDescriptor
                        {
                            SourcePath = buildEngineSpecification._enginePath,
                            WorkspaceDisambiguators = new[] { workspaceSuffix },
                        },
                        cancellationToken);
                }
                else
                {
                    return await _workspaceProvider.GetWorkspaceAsync(
                        new FolderAliasWorkspaceDescriptor
                        {
                            AliasedPath = buildEngineSpecification._enginePath
                        },
                        cancellationToken);
                }
            }
            else if (buildEngineSpecification._uefsPackageTag != null)
            {
                return await _workspaceProvider.GetWorkspaceAsync(
                    new UefsPackageWorkspaceDescriptor
                    {
                        PackageTag = buildEngineSpecification._uefsPackageTag,
                        WorkspaceDisambiguators = new[] { workspaceSuffix },
                    },
                    cancellationToken);
            }
            else if (buildEngineSpecification._gitCommit != null)
            {
                return await _workspaceProvider.GetWorkspaceAsync(
                    new GitWorkspaceDescriptor
                    {
                        RepositoryUrl = buildEngineSpecification._gitUrl!,
                        RepositoryCommitOrRef = buildEngineSpecification._gitCommit,
                        AdditionalFolderLayers = Array.Empty<string>(),
                        AdditionalFolderZips = buildEngineSpecification._gitConsoleZips!,
                        WorkspaceDisambiguators = new[] { workspaceSuffix },
                        ProjectFolderName = null,
                        IsEngineBuild = buildEngineSpecification._isEngineBuild,
                    },
                    cancellationToken);
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }
}
