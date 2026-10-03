using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ombi.Core.Services
{
    public interface IBackgroundNotificationQueue
    {
        bool TryQueue(string description, Func<IServiceProvider, Task> notification);
        IAsyncEnumerable<BackgroundNotificationWorkItem> ReadAllAsync();
        void Complete();
    }

    public sealed class BackgroundNotificationWorkItem
    {
        public BackgroundNotificationWorkItem(string description, Func<IServiceProvider, Task> executeAsync)
        {
            Description = description;
            ExecuteAsync = executeAsync;
        }

        public string Description { get; }
        public Func<IServiceProvider, Task> ExecuteAsync { get; }
    }
}
