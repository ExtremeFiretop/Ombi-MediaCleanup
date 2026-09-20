using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MockQueryable.Moq;
using Moq;
using NUnit.Framework;
using Ombi.Controllers.V1;
using Ombi.Store.Entities;
using Ombi.Store.Entities.Requests;
using Ombi.Store.Repository;
using Ombi.Store.Repository.Requests;

namespace Ombi.Tests
{
    [TestFixture]
    public class RequestRetryControllerTests
    {
        [Test]
        public async Task GetFailedRequests_SkipsOrphanedMovieQueueEntry()
        {
            var queue = new List<RequestQueue>
            {
                new RequestQueue
                {
                    RequestId = 42,
                    Type = RequestType.Movie,
                    Dts = DateTime.UtcNow,
                    Error = "Original request was deleted",
                    RetryCount = 1
                }
            };

            var queueRepository = new Mock<IRepository<RequestQueue>>();
            queueRepository.Setup(x => x.GetAll()).Returns(queue.AsQueryable().BuildMock());

            var movieRepository = new Mock<IMovieRequestRepository>();
            movieRepository.Setup(x => x.Find(It.IsAny<object>())).ReturnsAsync((MovieRequests)null);

            var subject = new RequestRetryController(
                queueRepository.Object,
                movieRepository.Object,
                Mock.Of<ITvRequestRepository>(),
                Mock.Of<IMusicRequestRepository>());

            var result = (await subject.GetFailedRequests()).ToList();

            Assert.That(result, Is.Empty);
        }
    }
}
