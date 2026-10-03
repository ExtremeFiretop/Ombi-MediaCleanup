using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Ombi.Core.Services
{
    public sealed class BackgroundNotificationQueue : IBackgroundNotificationQueue
    {
        private readonly Channel<BackgroundNotificationWorkItem> _queue =
            Channel.CreateUnbounded<BackgroundNotificationWorkItem>(
                new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false,
                    AllowSynchronousContinuations = false
                });

        public bool TryQueue(string description, Func<IServiceProvider, Task> notification)
        {
            ArgumentNullException.ThrowIfNull(notification);

            var workItem = new BackgroundNotificationWorkItem(
                string.IsNullOrWhiteSpace(description) ? "Background notification" : description,
                notification);

            return _queue.Writer.TryWrite(workItem);
        }

        public IAsyncEnumerable<BackgroundNotificationWorkItem> ReadAllAsync()
        {
            return _queue.Reader.ReadAllAsync();
        }

        public void Complete()
        {
            _queue.Writer.TryComplete();
        }
    }
}
