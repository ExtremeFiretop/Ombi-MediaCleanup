using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using Ombi.Controllers.V1;
using Ombi.Store.Context;
using Ombi.Store.Entities;
using Ombi.Store.Entities.Requests;
using Ombi.Store.Repository;
using Ombi.Store.Repository.Requests;

namespace Ombi.Tests
{
    [TestFixture]
    public class RequestRetryControllerTests
    {
        private sealed class TestOmbiContext : OmbiContext
        {
            public TestOmbiContext(DbContextOptions<TestOmbiContext> options) : base(options)
            {
            }
        }

        [Test]
        public async Task GetFailedRequests_SkipsOrphanedMovieQueueEntry()
        {
            var options = new DbContextOptionsBuilder<TestOmbiContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            await using var context = new TestOmbiContext(options);
            var queueRepository = new Repository<RequestQueue>(context);
            await queueRepository.Add(new RequestQueue
            {
                RequestId = 42,
                Type = RequestType.Movie,
                Dts = DateTime.UtcNow,
                Error = "Original request was deleted",
                RetryCount = 1
            });

            var movieRepository = new Mock<IMovieRequestRepository>();
            movieRepository.Setup(x => x.Find(It.IsAny<object>())).ReturnsAsync((MovieRequests)null);

            var subject = new RequestRetryController(
                queueRepository,
                movieRepository.Object,
                Mock.Of<ITvRequestRepository>(),
                Mock.Of<IMusicRequestRepository>());

            var result = (await subject.GetFailedRequests()).ToList();

            Assert.That(result, Is.Empty);
        }
    }
}
