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
                if (vm.SeasonRequests.Any())
                {
                    var requestTheMovieDbId = vm.RequestTheMovieDbId > 0
                        ? vm.RequestTheMovieDbId
                        : vm.Id;
                    var requestTvDbId = vm.RequestTvDbId;

                    // Sonarr is TVDB-centric and older cache rows commonly have TheMovieDbId = 0.
                    // Never query either cache with a zero provider ID: doing so groups unrelated
                    // series together and can incorrectly remove every episode from a new request.
                    var monitoredEpisodes = new HashSet<(int SeasonNumber, int EpisodeNumber)>();

                    if (requestTvDbId > 0)
                    {
                        var tvDbEpisodes = await _ctx.SonarrEpisodeCache
                            .AsNoTracking()
                            .Where(x => x.TvDbId == requestTvDbId)
                            .Select(x => new { x.SeasonNumber, x.EpisodeNumber })
                            .ToListAsync();
                        monitoredEpisodes.UnionWith(
                            tvDbEpisodes.Select(x => (x.SeasonNumber, x.EpisodeNumber)));
                    }

                    // Fall back to TMDB only when it is a real provider ID and TVDB did not yield
                    // any cached episodes. This keeps compatibility with newer Sonarr cache rows.
                    if (monitoredEpisodes.Count == 0 && requestTheMovieDbId > 0)
                    {
                        var movieDbEpisodes = await _ctx.SonarrEpisodeCache
                            .AsNoTracking()
                            .Where(x => x.MovieDbId == requestTheMovieDbId)
                            .Select(x => new { x.SeasonNumber, x.EpisodeNumber })
                            .ToListAsync();
                        monitoredEpisodes.UnionWith(
                            movieDbEpisodes.Select(x => (x.SeasonNumber, x.EpisodeNumber)));
                    }

                    if (monitoredEpisodes.Count > 0)
                    {
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