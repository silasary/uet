﻿namespace Redpoint.Uet.Core
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Logging.SingleLine;
    using Redpoint.Uet.Core.Permissions;
    using Serilog;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;

    public static class CoreServiceExtensions
    {
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
             Justification = "AddConsoleFormatter and RegisterProviderOptions are only dangerous when the Options type cannot be statically analyzed, but that is not the case here. " +
             "The DynamicallyAccessedMembers annotations on them will make sure to preserve the right members from the different options objects.")]
        public static void AddUETCore(
            this IServiceCollection services,
            bool omitLogPrefix = false,
            LogLevel minimumLogLevel = LogLevel.Information,
            bool skipLoggingRegistration = false,
            bool permitRunbackLogging = false)
        {
            services.AddSingleton<IStringUtilities, DefaultStringUtilities>();
            services.AddSingleton<IWorldPermissionApplier, DefaultWorldPermissionApplier>();

            if (!skipLoggingRegistration)
            {
                services.AddLogging(builder =>
                {
                    var enableRunbackLogging = permitRunbackLogging && Environment.GetEnvironmentVariable("UET_RUNBACKS") == "1";
                    if (enableRunbackLogging)
                    {
                        builder.ClearProviders();
                        builder.SetMinimumLevel(LogLevel.Trace);
                        builder.AddSingleLineConsoleFormatter(options =>
                        {
                            options.OmitLogPrefix = omitLogPrefix;
                        });
                        builder.AddSingleLineConsole(options =>
                        {
                            options.IncludeTracing = minimumLogLevel == LogLevel.Trace;
                        });
                        Directory.CreateDirectory(RunbackGlobalState.RunbackDirectoryPath);
                        var logger = new LoggerConfiguration()
                            .MinimumLevel.Verbose()
                            .WriteTo.File(RunbackGlobalState.RunbackLogPath, formatProvider: CultureInfo.InvariantCulture)
                            .CreateLogger();
                        builder.AddSerilog(logger, dispose: true);
                    }
                    else
                    {
                        builder.ClearProviders();
                        builder.SetMinimumLevel(minimumLogLevel);
                        builder.AddSingleLineConsoleFormatter(options =>
                        {
                            options.OmitLogPrefix = omitLogPrefix;
                        });
                        builder.AddSingleLineConsole();
                    }
                });
            }
        }
    }
}