using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Ombi.Api.External.ExternalApis.TheMovieDb;
using Ombi.Api.External.ExternalApis.TheMovieDb.Models;
using Ombi.Core.Helpers;
using Ombi.Core.Models.Requests;
using Ombi.Store.Entities.Requests;

namespace Ombi.Core.Tests
{
    [TestFixture]
    public class TvShowRequestBuilderV2Tests
    {

        [Test]
        public async Task CreateChild_StoresProviderIdentityWithoutUsingPrimaryKey()
        {
            var movieDb = new Mock<IMovieDbApi>();
            movieDb.Setup(x => x.GetTVInfo("335840", "en")).ReturnsAsync(new TvInfo
            {
                id = 335840,
                name = "Monster",
                first_air_date = "2022-09-21",
                seasons = new List<Season>(),
                ExternalIds = new ExternalIds
                {
                    ImdbId = "tt13207736",
                    TvDbId = "389492"
                }
            });

            var subject = new TvShowRequestBuilderV2(movieDb.Object);
            var result = await subject.GetShowInfo(335840, "en");

            result.CreateChild(new TvRequestViewModelV2 { TheMovieDbId = 335840 }, "user-1", RequestSource.PlexWatchlist);

            Assert.That(result.ChildRequest.Id, Is.Zero);
            Assert.That(result.ChildRequest.RequestTheMovieDbId, Is.EqualTo(335840));
            Assert.That(result.ChildRequest.RequestTvDbId, Is.EqualTo(389492));
            Assert.That(result.ChildRequest.RequestImdbId, Is.EqualTo("tt13207736"));
        }


        [Test]
        public async Task GetShowInfo_PreservesImdbId_WhenTvDbMappingIsStillMissing()
        {
            var movieDb = new Mock<IMovieDbApi>();
            movieDb.Setup(x => x.GetTVInfo("335840", "en")).ReturnsAsync(new TvInfo
            {
                id = 335840,
                name = "Monster",
                first_air_date = "2022-09-21",
                seasons = new List<Season>(),
                ExternalIds = new ExternalIds()
            });
            movieDb.Setup(x => x.GetTvExternals(335840)).ReturnsAsync(new TvExternals
            {
                imdb_id = "tt13207736",
                tvdb_id = 0
            });

            var subject = new TvShowRequestBuilderV2(movieDb.Object);
            var result = await subject.GetShowInfo(335840, "en");

            result.CreateChild(new TvRequestViewModelV2 { TheMovieDbId = 335840 }, "user-1", RequestSource.PlexWatchlist);

            Assert.That(result.ChildRequest.RequestTvDbId, Is.Zero);
            Assert.That(result.ChildRequest.RequestImdbId, Is.EqualTo("tt13207736"));
        }

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
