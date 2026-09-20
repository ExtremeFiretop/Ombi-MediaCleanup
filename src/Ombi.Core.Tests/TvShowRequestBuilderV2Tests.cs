using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Ombi.Api.External.ExternalApis.TheMovieDb;
using Ombi.Api.External.ExternalApis.TheMovieDb.Models;
using Ombi.Core.Helpers;

namespace Ombi.Core.Tests
{
    [TestFixture]
    public class TvShowRequestBuilderV2Tests
    {
        [Test]
        public async Task GetShowInfo_WhenTmdbReturnsNull_ReturnsNullInsteadOfThrowing()
        {
            var movieDb = new Mock<IMovieDbApi>();
            movieDb.Setup(x => x.GetTVInfo("250308", "en")).ReturnsAsync((TvInfo)null);

            var subject = new TvShowRequestBuilderV2(movieDb.Object);

            var result = await subject.GetShowInfo(250308, "en");

            Assert.That(result, Is.Null);
        }
    }
}
