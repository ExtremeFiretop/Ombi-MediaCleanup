using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Moq.AutoMock;
using NUnit.Framework;
using Ombi.Api.External.ExternalApis.TheMovieDb;
using Ombi.Api.External.ExternalApis.TheMovieDb.Models;
using Ombi.Core.Authentication;
using Ombi.Core.Engine;
using Ombi.Core.Helpers;
using Ombi.Core.Models.Requests;
using Ombi.Core.Rule;
using Ombi.Core.Rule.Interfaces;
using Ombi.Store.Entities;
using Ombi.Store.Repository.Requests;
using Ombi.Test.Common;

namespace Ombi.Core.Tests.Engine
{
    [TestFixture]
    public class TvRequestEngineV2Tests
    {
        [Test]
        public async Task RequestTvShow_PreservesRuleErrorCode()
        {
            var mocker = new AutoMocker();
            var user = new OmbiUser { Id = "user-1", NormalizedUserName = "TEST" };
            var userManager = MockHelper.MockUserManager(new List<OmbiUser> { user });
            userManager.Setup(x => x.IsInRoleAsync(It.IsAny<OmbiUser>(), It.IsAny<string>())).ReturnsAsync(true);

            var currentUser = new Mock<ICurrentUser>();
            currentUser.Setup(x => x.Username).Returns("Test");
            currentUser.Setup(x => x.GetUser()).ReturnsAsync(user);

            var requestService = new Mock<IRequestServiceMain>();
            requestService.Setup(x => x.TvRequestService).Returns(new Mock<ITvRequestRepository>().Object);
            requestService.Setup(x => x.MovieRequestService).Returns(new Mock<IMovieRequestRepository>().Object);
            requestService.Setup(x => x.MusicRequestRepository).Returns(new Mock<IMusicRequestRepository>().Object);

            mocker.Use(currentUser.Object);
            mocker.Use(userManager.Object);
            mocker.Use(requestService.Object);

            mocker.GetMock<IMovieDbApi>()
                .Setup(x => x.GetTVInfo("335840", "en"))
                .ReturnsAsync(new TvInfo
                {
                    id = 335840,
                    name = "Monster",
                    first_air_date = "2022-09-21",
                    seasons = new List<Season>
                    {
                        new Season { season_number = 1 }
                    },
                    ExternalIds = new ExternalIds
                    {
                        ImdbId = "tt13207736",
                        TvDbId = "389492"
                    }
                });

            mocker.GetMock<IMovieDbApi>()
                .Setup(x => x.GetSeasonEpisodes(335840, 1, It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ReturnsAsync(new SeasonDetails
                {
                    season_number = 1,
                    episodes = new[]
                    {
                        new Episode
                        {
                            season_number = 1,
                            episode_number = 1,
                            name = "Episode 1"
                        }
                    }
                });

            mocker.GetMock<IRuleEvaluator>()
                .Setup(x => x.StartRequestRules(It.IsAny<Ombi.Store.Entities.Requests.BaseRequest>()))
                .ReturnsAsync(new[]
                {
                    new RuleResult
                    {
                        Success = false,
                        ErrorCode = ErrorCode.EpisodesAlreadyRequested,
                        Message = "We already have episodes requested from series Monster"
                    }
                });

            var subject = mocker.CreateInstance<TvRequestEngine>();
            var result = await subject.RequestTvShow(new TvRequestViewModelV2
            {
                TheMovieDbId = 335840,
                Seasons = new List<SeasonsViewModel>
                {
                    new SeasonsViewModel
                    {
                        SeasonNumber = 1,
                        Episodes = new List<EpisodesViewModel>
                        {
                            new EpisodesViewModel { EpisodeNumber = 1 }
                        }
                    }
                }
            });

            Assert.That(result.IsError, Is.True);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCode.EpisodesAlreadyRequested));
            Assert.That(result.ErrorMessage, Is.EqualTo("We already have episodes requested from series Monster"));
        }
    }
}
