using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Ombi.Core.Engine;
using Ombi.Core.Models.Search;
using Ombi.Helpers;
using Ombi.Store.Context;
using Ombi.Store.Entities;
using Ombi.Store.Entities.Requests;
using Ombi.Store.Repository.Requests;

namespace Ombi.Core.Rule.Rules
{
    public class SonarrCacheRule
    {
        public SonarrCacheRule(ExternalContext ctx)
        {
            _ctx = ctx;
        }

        private readonly ExternalContext _ctx;

        public async Task<RuleResult> Execute(BaseRequest obj)
        {
            if (obj.RequestType == RequestType.TvShow)
            {
                var vm = (ChildRequests) obj;
                var existsInSonarr = await _ctx.SonarrCache
                    .AsNoTracking()
                    .AnyAsync(x => x.TheMovieDbId == vm.Id);
                if (existsInSonarr && vm.SeasonRequests.Any())
                {
                    // Load the show's Sonarr episode cache once. The previous implementation
                    // executed a database query for every requested episode.
                    var sonarrEpisodes = await _ctx.SonarrEpisodeCache
                        .AsNoTracking()
                        .Where(x => x.MovieDbId == vm.Id)
                        .Select(x => new { x.SeasonNumber, x.EpisodeNumber })
                        .ToListAsync();
                    var monitoredEpisodes = sonarrEpisodes
                        .Select(x => (x.SeasonNumber, x.EpisodeNumber))
                        .ToHashSet();

                    foreach (var season in vm.SeasonRequests)
                    {
                        season.Episodes.RemoveAll(ep =>
                            monitoredEpisodes.Contains((season.SeasonNumber, ep.EpisodeNumber)));
                    }

                    var anyEpisodes = vm.SeasonRequests.SelectMany(x => x.Episodes).Any();
                    if (!anyEpisodes)
                    {
                        return new RuleResult { ErrorCode = ErrorCode.EpisodesAlreadyRequested, Message = $"We already have episodes requested from series {vm.Title}" };
                    }
                }
            }
            return new RuleResult { Success = true };
        }

        public async Task<RuleResult> Execute(SearchViewModel obj)
        {
            if (obj.Type == RequestType.TvShow)
            {
                var vm = (SearchTvShowViewModel) obj;
                // Check if it's in Sonarr
                if (!vm.TheTvDbId.HasValue())
                {
                    return new RuleResult { Success = true };
                }
                var tvdbidint = int.Parse(vm.TheTvDbId);
                var existsInSonarr = await _ctx.SonarrCache
                    .AsNoTracking()
                    .AnyAsync(x => x.TvDbId == tvdbidint);
                if (existsInSonarr)
                {
                    vm.Approved = true;

                    if (vm.SeasonRequests.Any())
                    {
                        // Fetch every cached Sonarr episode for this show in one query, then
                        // evaluate the TMDB episode list in memory. This removes the per-episode
                        // EF query pattern that made Discover very expensive for long-running shows.
                        var sonarrEpisodes = await _ctx.SonarrEpisodeCache
                            .AsNoTracking()
                            .Where(x => x.TvDbId == tvdbidint)
                            .Select(x => new { x.SeasonNumber, x.EpisodeNumber, x.HasFile })
                            .ToListAsync();

                        var monitoredEpisodes = sonarrEpisodes
                            .Select(x => (x.SeasonNumber, x.EpisodeNumber))
                            .ToHashSet();
                        var episodesWithFiles = sonarrEpisodes
                            .Where(x => x.HasFile)
                            .Select(x => (x.SeasonNumber, x.EpisodeNumber))
                            .ToHashSet();

                        foreach (var season in vm.SeasonRequests)
                        {
                            foreach (var ep in season.Episodes)
                            {
                                var episodeKey = (season.SeasonNumber, ep.EpisodeNumber);
                                if (!monitoredEpisodes.Contains(episodeKey))
                                {
                                    continue;
                                }

                                ep.Approved = true;
                                if (episodesWithFiles.Contains(episodeKey))
                                {
                                    obj.Available = true;
                                }
                            }
                        }
                    }
                }
            }
            return new RuleResult { Success = true };
        }
    }
}