using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.ScheduledTasks.Tasks
{
    /// <summary>
    /// Optimizes Jellyfin's database by issuing a VACUUM command.
    /// </summary>
    public class OptimizeDatabaseTask : IScheduledTask, IConfigurableScheduledTask
    {
        private readonly ILogger<OptimizeDatabaseTask> _logger;
        private readonly ILocalizationManager _localization;
        private readonly IDbContextFactory<JellyfinDb> _provider;

        /// <summary>
        /// Initializes a new instance of the <see cref="OptimizeDatabaseTask" /> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="localization">The localization manager.</param>
        /// <param name="provider">The jellyfin DB context provider.</param>
        public OptimizeDatabaseTask(
            ILogger<OptimizeDatabaseTask> logger,
            ILocalizationManager localization,
            IDbContextFactory<JellyfinDb> provider)
        {
            _logger = logger;
            _localization = localization;
            _provider = provider;
        }

        /// <inheritdoc />
        public string Name => _localization.GetLocalizedString("TaskOptimizeDatabase");

        /// <inheritdoc />
        public string Description => _localization.GetLocalizedString("TaskOptimizeDatabaseDescription");

        /// <inheritdoc />
        public string Category => _localization.GetLocalizedString("TasksMaintenanceCategory");

        /// <inheritdoc />
        public string Key => "OptimizeDatabaseTask";

        /// <inheritdoc />
        public bool IsHidden => false;

        /// <inheritdoc />
        public bool IsEnabled => true;

        /// <inheritdoc />
        public bool IsLogged => true;

        /// <summary>
        /// Creates the triggers that define when the task will run.
        /// </summary>
        /// <returns>IEnumerable{BaseTaskTrigger}.</returns>
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                // Every so often
                new TaskTriggerInfo { Type = TaskTriggerInfo.TriggerInterval, IntervalTicks = TimeSpan.FromHours(24).Ticks }
            };
        }

        /// <inheritdoc />
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Restarting WAL checkpoint & Optimizing jellyfin.db (BASIC)...");

            try
            {
                var context = await _provider.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                await using (context.ConfigureAwait(false))
                {
                    if (context.Database.IsSqlite())
                    {
                        await context.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(RESTART);", cancellationToken).ConfigureAwait(false);
                        await context.Database.ExecuteSqlRawAsync("PRAGMA quick_check(1);", cancellationToken).ConfigureAwait(false);
                        await context.Database.ExecuteSqlRawAsync("PRAGMA analysis_limit=1024; PRAGMA optimize; REINDEX;", cancellationToken).ConfigureAwait(false);
                        _logger.LogInformation("jellyfin.db optimized successfully!");
                    }
                    else
                    {
                        _logger.LogInformation("This database doesn't support optimization");
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while optimizing jellyfin.db");
            }
        }
    }
}
