using System;
using System.Collections.Generic;
using System.Linq;
using Ombi.Api.External.ExternalApis.Sonarr.Models;
using Ombi.Store.Repository.Requests;

namespace Ombi.Core.Senders
{
    /// <summary>
    /// Resolves a TMDB-style requested season to the corresponding Sonarr season when providers
    /// disagree about whether an anthology season is a standalone show. Matches are intentionally
    /// conservative: at least three requested episode titles must identify exactly one Sonarr season.
    /// </summary>
    public static class SonarrEpisodeFingerprintMatcher
    {
        private const int MinimumEpisodeFingerprintSize = 3;

        public static SonarrSeasonFingerprintMatch FindSingleSeasonMatch(
            SeasonRequests sourceSeason,
            IEnumerable<Episode> sonarrEpisodes)
        {
            var fingerprint = GetFingerprint(sourceSeason);
            if (fingerprint.Count < MinimumEpisodeFingerprintSize)
            {
                return null;
            }

            var episodes = sonarrEpisodes?
                .Where(x => x != null)
                .ToList() ?? new List<Episode>();

            if (episodes.Count == 0)
            {
                return null;
            }

            // Use the longest title as an anchor to keep the candidate set small. The final
            // decision still requires every requested episode title/number to match.
            var anchor = fingerprint
                .OrderByDescending(x => x.NormalizedTitle.Length)
                .First();

            var candidateSeasons = episodes
                .Where(x => x.episodeNumber == anchor.EpisodeNumber &&
                            TitlesMatch(x.title, anchor.NormalizedTitle))
                .Select(x => x.seasonNumber)
                .Distinct()
                .ToList();

            var matches = new List<int>();
            foreach (var candidateSeason in candidateSeasons)
            {
                var candidateByNumber = episodes
                    .Where(x => x.seasonNumber == candidateSeason)
                    .GroupBy(x => x.episodeNumber)
                    .ToDictionary(x => x.Key, x => x.First().title);

                var fullFingerprintMatches = fingerprint.All(expected =>
                    candidateByNumber.TryGetValue(expected.EpisodeNumber, out var actualTitle) &&
                    TitlesMatch(actualTitle, expected.NormalizedTitle));

                if (fullFingerprintMatches)
                {
                    matches.Add(candidateSeason);
                }
            }

            // Never guess when more than one Sonarr season satisfies the fingerprint.
            if (matches.Count != 1)
            {
                return null;
            }

            return new SonarrSeasonFingerprintMatch
            {
                SourceSeasonNumber = sourceSeason.SeasonNumber,
                SonarrSeasonNumber = matches[0]
            };
        }

        /// <summary>
        /// Returns true when Sonarr contains the source season number but its episode titles do not
        /// describe the requested season. This protects anthology mappings from silently targeting
        /// the wrong season when a safe fingerprint remap cannot be established.
        /// </summary>
        public static bool HasConflictingExactSeason(
            SeasonRequests sourceSeason,
            IEnumerable<Episode> sonarrEpisodes)
        {
            var fingerprint = GetFingerprint(sourceSeason);
            if (fingerprint.Count == 0)
            {
                return false;
            }

            var exactSeason = (sonarrEpisodes ?? Enumerable.Empty<Episode>())
                .Where(x => x != null && x.seasonNumber == sourceSeason.SeasonNumber)
                .GroupBy(x => x.episodeNumber)
                .ToDictionary(x => x.Key, x => x.First().title);

            if (exactSeason.Count == 0)
            {
                return false;
            }

            return fingerprint.Any(expected =>
                !exactSeason.TryGetValue(expected.EpisodeNumber, out var actualTitle) ||
                !TitlesMatch(actualTitle, expected.NormalizedTitle));
        }

        private static List<EpisodeFingerprint> GetFingerprint(SeasonRequests sourceSeason)
        {
            return sourceSeason?.Episodes?
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Title))
                .GroupBy(x => x.EpisodeNumber)
                .Select(x => x.First())
                .Select(x => new EpisodeFingerprint
                {
                    EpisodeNumber = x.EpisodeNumber,
                    NormalizedTitle = NormalizeTitle(x.Title)
                })
                .Where(x => !string.IsNullOrEmpty(x.NormalizedTitle))
                .OrderBy(x => x.EpisodeNumber)
                .ToList() ?? new List<EpisodeFingerprint>();
        }

        private static bool TitlesMatch(string actualTitle, string normalizedExpectedTitle)
        {
            return string.Equals(
                NormalizeTitle(actualTitle),
                normalizedExpectedTitle,
                StringComparison.Ordinal);
        }

        private static string NormalizeTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return string.Empty;
            }

            return new string(title
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
        }

        private sealed class EpisodeFingerprint
        {
            public int EpisodeNumber { get; set; }
            public string NormalizedTitle { get; set; }
        }
    }

    public sealed class SonarrSeasonFingerprintMatch
    {
        public int SourceSeasonNumber { get; set; }
        public int SonarrSeasonNumber { get; set; }
    }
}
