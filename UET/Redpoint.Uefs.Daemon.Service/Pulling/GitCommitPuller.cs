﻿namespace Redpoint.Uefs.Daemon.Service.Pulling
{
    using Redpoint.Uefs.Daemon.Abstractions;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using Redpoint.Uefs.Daemon.Transactional.Executors;
    using Redpoint.Uefs.Protocol;
    using System.Threading;
    using System.Threading.Tasks;
    using Redpoint.Concurrency;

    internal class GitCommitPuller : IPuller<PullGitCommitRequest>
    {
        public async Task<PullResult> PullAsync(
            IUefsDaemon daemon,
            PullGitCommitRequest request,
            TransactionListenerDelegate<FileInfo?> onPollingResponse,
            CancellationToken cancellationToken)
        {
            // Run pull transaction in case we need to pull this Git commit.
            await using ((await daemon.TransactionalDatabase.BeginTransactionAsync(
                new PullGitCommitTransactionRequest
                {
                    GitRepoManager = daemon.PackageStorage.GitRepoManager,
                    GitUrl = request.Url,
                    GitCommit = request.Commit,
                    Credential = request.Credential,
                    NoWait = request.PullRequest.NoWait,
                },
                response => onPollingResponse(response, null),
                cancellationToken).ConfigureAwait(false)).AsAsyncDisposable(out var transaction).ConfigureAwait(false))
            {
                // If we're not waiting, go as soon as the operation ID is available.
                if (request.PullRequest.NoWait)
                {
                    return new PullResult(transaction.TransactionId, false);
                }

                await transaction.WaitForCompletionAsync(cancellationToken).ConfigureAwait(false);
                return new PullResult(transaction.TransactionId, true);
            }
        }
    }
}
