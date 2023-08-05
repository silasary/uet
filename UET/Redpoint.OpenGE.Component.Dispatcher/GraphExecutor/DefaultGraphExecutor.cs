﻿namespace Redpoint.OpenGE.Component.Dispatcher.GraphExecutor
{
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using Redpoint.OpenGE.Component.Dispatcher.Graph;
    using Redpoint.OpenGE.Component.Dispatcher.WorkerPool;
    using Redpoint.OpenGE.Protocol;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Threading.Tasks;

    internal class DefaultGraphExecutor : IGraphExecutor
    {
        private readonly ILogger<DefaultGraphExecutor> _logger;

        public DefaultGraphExecutor(
            ILogger<DefaultGraphExecutor> logger)
        {
            _logger = logger;
        }

        private class GraphExecutionInstance
        {
            public required IWorkerPool WorkerPool;
            public long RemainingTasks;
            public readonly SemaphoreSlim QueuedTaskAvailableForScheduling = new SemaphoreSlim(0);
            public readonly ConcurrentQueue<GraphTask> QueuedTasksForScheduling = new ConcurrentQueue<GraphTask>();
            public CancellationTokenSource CancellationTokenSource { get; private init; }
            public CancellationToken CancellationToken => CancellationTokenSource.Token;
            public readonly List<Task> ScheduledExecutions = new List<Task>();
            public readonly SemaphoreSlim ScheduledExecutionsLock = new SemaphoreSlim(1);
            public readonly HashSet<GraphTask> ScheduledTasks = new HashSet<GraphTask>();
            public readonly HashSet<GraphTask> CompletedTasks = new HashSet<GraphTask>();
            public readonly SemaphoreSlim CompletedAndScheduledTasksLock = new SemaphoreSlim(1);

            public GraphExecutionInstance(CancellationToken cancellationToken)
            {
                CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);
            }
        }

        public async Task ExecuteGraphAsync(
            IWorkerPool workerPool,
            Graph graph, 
            IAsyncStreamWriter<JobResponse> responseStream,
            CancellationToken cancellationToken)
        {
            var graphStopwatch = Stopwatch.StartNew();

            // Track the state of this graph execution.
            var instance = new GraphExecutionInstance(cancellationToken)
            {
                WorkerPool = workerPool,
                RemainingTasks = graph.Tasks.Count,
            };

            // Schedule up all of the tasks that can be immediately
            // scheduled.
            foreach (var taskKv in graph.Tasks)
            {
                if (graph.TaskDependencies.WhatTargetDependsOn(taskKv.Value).Count == 0)
                {
                    instance.QueuedTasksForScheduling.Enqueue(taskKv.Value);
                    instance.QueuedTaskAvailableForScheduling.Release();
                    // @note: We don't take a lock here because nothing else will
                    // be accessing it yet.
                    instance.ScheduledTasks.Add(taskKv.Value);
                }
            }

            // At this point, if we don't have anything that can be
            // scheduled, then the execution can never make any progress.
            if (instance.QueuedTasksForScheduling.Count == 0)
            {
                throw new RpcException(new Status(
                    StatusCode.InvalidArgument, 
                    "No task described by the job XML was immediately schedulable."));
            }

            // Pull tasks off the queue until we have no tasks remaining.
            while (!instance.CancellationToken.IsCancellationRequested &&
                   instance.RemainingTasks > 0)
            {
                // Get the next task to schedule. This queue only contains
                // tasks whose dependencies have all passed.
                await instance.QueuedTaskAvailableForScheduling.WaitAsync(
                    instance.CancellationToken);
                if (Interlocked.Read(ref instance.RemainingTasks) == 0)
                {
                    break;
                }
                if (!instance.QueuedTasksForScheduling.TryDequeue(out var task))
                {
                    throw new RpcException(new Status(
                        StatusCode.Internal,
                        "Queued task semaphore indicated a task could be scheduled, but nothing was available in the queue."));
                }

                // Schedule up a background 
                instance.ScheduledExecutions.Add(Task.Run(async () =>
                {
                    var status = TaskCompletionStatus.TaskCompletionException;
                    var exitCode = 1;
                    var exceptionMessage = string.Empty;
                    var didStart = false;
                    var didComplete = false;
                    var taskStopwatch = new Stopwatch();
                    try
                    {
                        try
                        {
                            // Reserve a core from somewhere...
                            await using var core = await instance.WorkerPool.ReserveCoreAsync(
                                task.TaskDescriptor.DescriptorCase != TaskDescriptor.DescriptorOneofCase.Remote,
                                instance.CancellationToken);

                            // We're now going to start doing the work for this task.
                            taskStopwatch.Start();
                            await responseStream.WriteAsync(new JobResponse
                            {
                                TaskStarted = new TaskStartedResponse
                                {
                                    Id = task.GraphTaskSpec.Task.Name,
                                    DisplayName = task.GraphTaskSpec.Task.Caption,
                                    WorkerMachineName = core.WorkerMachineName,
                                    WorkerCoreNumber = core.WorkerCoreNumber,
                                },
                            }, instance.CancellationToken);
                            didStart = true;

                            // Perform synchronisation for remote tasks.
                            if (task.TaskDescriptor.DescriptorCase == TaskDescriptor.DescriptorOneofCase.Remote)
                            {
                                // @todo: Implement tool and blob synchronisation.
                            }

                            // Execute the task on the core.
                            await core.Request.RequestStream.WriteAsync(new ExecutionRequest
                            {
                                ExecuteTask = new ExecuteTaskRequest
                                {
                                    Descriptor_ = task.TaskDescriptor,
                                }
                            }, instance.CancellationToken);

                            // Stream the results until we get an exit code.
                            while (!didComplete &&
                                await core.Request.ResponseStream.MoveNext(instance.CancellationToken))
                            {
                                var current = core.Request.ResponseStream.Current;
                                if (current.ResponseCase != ExecutionResponse.ResponseOneofCase.ExecuteTask)
                                {
                                    throw new RpcException(new Status(
                                        StatusCode.InvalidArgument,
                                        "Unexpected task execution response from worker RPC."));
                                }
                                switch (current.ExecuteTask.Response.DataCase)
                                {
                                    case ProcessResponse.DataOneofCase.StandardOutputLine:
                                        await responseStream.WriteAsync(new JobResponse
                                        {
                                            TaskOutput = new TaskOutputResponse
                                            {
                                                Id = task.GraphTaskSpec.Task.Name,
                                                StandardOutputLine = current.ExecuteTask.Response.StandardOutputLine,
                                            }
                                        });
                                        break;
                                    case ProcessResponse.DataOneofCase.StandardErrorLine:
                                        await responseStream.WriteAsync(new JobResponse
                                        {
                                            TaskOutput = new TaskOutputResponse
                                            {
                                                Id = task.GraphTaskSpec.Task.Name,
                                                StandardErrorLine = current.ExecuteTask.Response.StandardErrorLine,
                                            }
                                        });
                                        break;
                                    case ProcessResponse.DataOneofCase.ExitCode:
                                        exitCode = current.ExecuteTask.Response.ExitCode;
                                        status = exitCode == 0
                                            ? TaskCompletionStatus.TaskCompletionSuccess
                                            : TaskCompletionStatus.TaskCompletionFailure;
                                        didComplete = true;
                                        break;
                                }
                            }
                        }
                        catch (OperationCanceledException) when (instance.CancellationTokenSource.IsCancellationRequested)
                        {
                            // We're stopping because something else cancelled the build.
                            status = TaskCompletionStatus.TaskCompletionCancelled;
                        }
                        catch (Exception ex)
                        {
                            status = TaskCompletionStatus.TaskCompletionException;
                            exceptionMessage = ex.ToString();
                        }
                    }
                    finally
                    {
                        try
                        {
                            if (!didStart)
                            {
                                // We never actually started this task because we failed
                                // to reserve, but we need to start it so we can then immediately
                                // convey the exception we ran into.
                                await responseStream.WriteAsync(new JobResponse
                                {
                                    TaskStarted = new TaskStartedResponse
                                    {
                                        Id = task.GraphTaskSpec.Task.Name,
                                        DisplayName = task.GraphTaskSpec.Task.Caption,
                                        WorkerMachineName = string.Empty,
                                        WorkerCoreNumber = 0,
                                    },
                                }, instance.CancellationToken);
                            }
                            await responseStream.WriteAsync(new JobResponse
                            {
                                TaskCompleted = new TaskCompletedResponse
                                {
                                    Id = task.GraphTaskSpec.Task.Name,
                                    Status = status,
                                    ExitCode = exitCode,
                                    ExceptionMessage = exceptionMessage,
                                    TotalSeconds = taskStopwatch.Elapsed.TotalSeconds,
                                }
                            }, instance.CancellationToken);

                            if (status == TaskCompletionStatus.TaskCompletionSuccess)
                            {
                                // This task succeeded, queue up downstream tasks for scheduling.
                                await instance.CompletedAndScheduledTasksLock.WaitAsync();
                                try
                                {
                                    instance.CompletedTasks.Add(task);
                                    if (Interlocked.Decrement(ref instance.RemainingTasks) != 0)
                                    {
                                        // What is waiting on this task?
                                        var dependsOn = graph.TaskDependencies.WhatDependsOnTarget(task);
                                        foreach (var depend in dependsOn)
                                        {
                                            // Is this task already scheduled or completed?
                                            if (instance.CompletedTasks.Contains(depend) ||
                                                instance.ScheduledTasks.Contains(depend))
                                            {
                                                continue;
                                            }

                                            // Are all the dependencies of this waiting task now satisified?
                                            var waitingOn = graph.TaskDependencies.WhatTargetDependsOn(depend);
                                            if (instance.CompletedTasks.IsSupersetOf(waitingOn))
                                            {
                                                // This task is now ready to schedule.
                                                instance.QueuedTasksForScheduling.Enqueue(depend);
                                                instance.QueuedTaskAvailableForScheduling.Release();
                                                instance.ScheduledTasks.Add(depend);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // Make sure our scheduling loop gets a chance to check
                                        // RemainingTasks.
                                        instance.QueuedTaskAvailableForScheduling.Release();
                                    }
                                }
                                finally
                                {
                                    instance.CompletedAndScheduledTasksLock.Release();
                                }
                            }
                            else
                            {
                                // This task failed, cancel the build.
                                instance.CancellationTokenSource.Cancel();
                            }
                        }
                        catch (Exception ex)
                        {
                            // If any of this fails, we have to cancel the build.
                            _logger.LogCritical(ex, ex.Message);
                            instance.CancellationTokenSource.Cancel();
                        }
                    }
                }));
            }

            if (instance.RemainingTasks == 0 &&
                instance.CompletedTasks.Count == graph.Tasks.Count)
            {
                // All tasks completed successfully.
                await responseStream.WriteAsync(new JobResponse
                {
                    JobComplete = new JobCompleteResponse
                    {
                        Status = JobCompletionStatus.JobCompletionSuccess,
                        TotalSeconds = graphStopwatch.Elapsed.TotalSeconds,
                    }
                });
            }
            else
            {
                // Something failed.
                await responseStream.WriteAsync(new JobResponse
                {
                    JobComplete = new JobCompleteResponse
                    {
                        Status = JobCompletionStatus.JobCompletionFailure,
                        TotalSeconds = graphStopwatch.Elapsed.TotalSeconds,
                    }
                });
            }
        }
    }
}
