using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ombi.Core.Services;

namespace Ombi.DependencyInjection
{
    public sealed class BackgroundNotificationHostedService : BackgroundService
    {
        private readonly IBackgroundNotificationQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BackgroundNotificationHostedService> _logger;

        public BackgroundNotificationHostedService(
            IBackgroundNotificationQueue queue,
            IServiceScopeFactory scopeFactory,
            ILogger<BackgroundNotificationHostedService> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Stop accepting new notifications when the host begins shutting down, then drain
            // work already queued. BackgroundService.StopAsync will wait for this task until the
            // host's normal shutdown timeout expires.
            using var stoppingRegistration = stoppingToken.Register(_queue.Complete);

            await foreach (var workItem in _queue.ReadAllAsync())
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    await workItem.ExecuteAsync(scope.ServiceProvider);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Background notification failed: {Description}", workItem.Description);
                }
            }
        }
    }
}
