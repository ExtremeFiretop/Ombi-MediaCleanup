using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Moq;
using Moq.AutoMock;
using NUnit.Framework;
using Ombi.Api.External.ExternalApis.TheMovieDb;
using Ombi.Api.External.ExternalApis.TheMovieDb.Models;
using Ombi.Core.Authentication;
using Ombi.Core.Engine.V2;
using Ombi.Core.Helpers;
using Ombi.Core.Models.Search;
using Ombi.Core.Rule;
using Ombi.Core.Rule.Interfaces;
using Ombi.Helpers;
using Ombi.Mapping.Profiles;
using Ombi.Store.Entities;
using Ombi.Test.Common;

namespace Ombi.Core.Tests.Engine.V2
{
    [TestFixture]
    public class TvSearchEngineV2Tests
    {
        [Test]
        public async Task GetShowInformation_ResolvesMissingExternalIdsBeforeAvailabilityRules()
        {
            var mocker = new AutoMocker();
            var userManager = MockHelper.MockUserManager(new List<OmbiUser>());
            mocker.Use(userManager.Object);

            mocker.GetMock<ICurrentUser>()
                .Setup(x => x.GetUser())
                .ReturnsAsync((OmbiUser)null);

            var mapperConfig = new MapperConfiguration(cfg => cfg.AddProfile<TvProfileV2>());
            mocker.Use(mapperConfig.CreateMapper());

            mocker.GetMock<ICacheService>()
                .Setup(x => x.GetOrAddAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<TvInfo>>>(),
                    It.IsAny<DateTimeOffset>()))
                .Returns((string cacheKey, Func<Task<TvInfo>> factory, DateTimeOffset expiration) => factory());

            mocker.GetMock<IMovieDbApi>()
                .Setup(x => x.GetTVInfo("299939", "en"))
                .ReturnsAsync(new TvInfo
                {
                    id = 299939,
                    name = "Monster: The Lizzie Borden Story",
                    first_air_date = "2026-09-17",
                    seasons = new List<Season>(),
                    networks = new[] { new Network { id = 213, name = "Netflix" } },
                    episode_run_time = Array.Empty<int>(),
                    genres = Array.Empty<Genre>(),
                    Credits = new Credits
                    {
                        cast = Array.Empty<FullMovieCast>(),
                        crew = Array.Empty<FullMovieCrew>()
                    },
                    Videos = new Videos { results = Array.Empty<Result>() },
                    Images = new Images
                    {
                        Backdrops = new List<ImageContent>(),
                        Posters = new List<ImageContent>
                        {
                            new ImageContent { FilePath = "/poster.jpg" }
                        }
                    },
                    ExternalIds = new ExternalIds
                    {
                        ImdbId = string.Empty,
                        TvDbId = null
                    }
                });

            mocker.GetMock<IMovieDbApi>()
                .Setup(x => x.GetTvExternals(299939))
                .ReturnsAsync(new TvExternals
                {
                    imdb_id = "tt13207736",
                    tvdb_id = 389492
                });

            SearchViewModel ruleInput = null;
            mocker.GetMock<IRuleEvaluator>()
                .Setup(x => x.StartSearchRules(It.IsAny<SearchViewModel>()))
                .Callback<SearchViewModel>(x =>
                {
                    ruleInput = x;
                    // This is the state PlexAvailabilityRule can now reach by matching
                    // the existing Plex row through the stable IMDb/TVDB identifiers.
                    if (x.ImdbId == "tt13207736")
                    {
                        x.Available = true;
                    }
                })
                .ReturnsAsync(new[] { new RuleResult { Success = true } });

            var subject = mocker.CreateInstance<TvSearchEngineV2>();
            var result = await subject.GetShowInformation("299939", CancellationToken.None);

            Assert.That(ruleInput, Is.Not.Null);
            Assert.That(ruleInput.ImdbId, Is.EqualTo("tt13207736"));
            Assert.That(ruleInput.TheTvDbId, Is.EqualTo("389492"));
            Assert.That(result.ImdbId, Is.EqualTo("tt13207736"));
            Assert.That(result.TheTvDbId, Is.EqualTo("389492"));
            Assert.That(result.Available, Is.True);

            mocker.GetMock<IMovieDbApi>()
                .Verify(x => x.GetTvExternals(299939), Times.Once);
        }
    }
}
