using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Ombi.Api.External.ExternalApis.Sonarr.Models;
using Ombi.Core.Senders;
using Ombi.Store.Repository.Requests;

namespace Ombi.Core.Tests.Senders
{
    [TestFixture]
    public class SonarrEpisodeFingerprintMatcherTests
    {
        [Test]
        public void StandaloneSeason_MapsToAnthologySeason_EvenWhenSameNumberExists()
        {
            var source = BuildSeason(1, LizzieTitles);
            var sonarrEpisodes = BuildEpisodes(1, DahmerTitles)
                .Concat(BuildEpisodes(4, LizzieTitles))
                .ToList();

            var result = SonarrEpisodeFingerprintMatcher.FindSingleSeasonMatch(source, sonarrEpisodes);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.SourceSeasonNumber, Is.EqualTo(1));
            Assert.That(result.SonarrSeasonNumber, Is.EqualTo(4));
            Assert.That(SonarrEpisodeFingerprintMatcher.HasConflictingExactSeason(source, sonarrEpisodes), Is.True);
        }

        [Test]
        public void NormalSeason_KeepsExactSeason_WhenEpisodeFingerprintMatches()
        {
            var source = BuildSeason(1, DahmerTitles);
            var sonarrEpisodes = BuildEpisodes(1, DahmerTitles)
                .Concat(BuildEpisodes(4, LizzieTitles))
                .ToList();

            var result = SonarrEpisodeFingerprintMatcher.FindSingleSeasonMatch(source, sonarrEpisodes);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.SonarrSeasonNumber, Is.EqualTo(1));
            Assert.That(SonarrEpisodeFingerprintMatcher.HasConflictingExactSeason(source, sonarrEpisodes), Is.False);
        }

        [Test]
        public void AmbiguousFingerprint_DoesNotGuess()
        {
            var source = BuildSeason(1, LizzieTitles.Take(3).ToArray());
            var sonarrEpisodes = BuildEpisodes(3, LizzieTitles.Take(3).ToArray())
                .Concat(BuildEpisodes(4, LizzieTitles.Take(3).ToArray()))
                .ToList();

            var result = SonarrEpisodeFingerprintMatcher.FindSingleSeasonMatch(source, sonarrEpisodes);

            Assert.That(result, Is.Null);
        }

        [Test]
        public void TooSmallFingerprint_WithConflictingExactSeason_IsDetectedAsUnsafe()
        {
            var source = BuildSeason(1, LizzieTitles.Take(2).ToArray());
            var sonarrEpisodes = BuildEpisodes(1, DahmerTitles)
                .Concat(BuildEpisodes(4, LizzieTitles))
                .ToList();

            var result = SonarrEpisodeFingerprintMatcher.FindSingleSeasonMatch(source, sonarrEpisodes);

            Assert.That(result, Is.Null);
            Assert.That(SonarrEpisodeFingerprintMatcher.HasConflictingExactSeason(source, sonarrEpisodes), Is.True);
        }

        private static SeasonRequests BuildSeason(int seasonNumber, IReadOnlyList<string> titles)
        {
            return new SeasonRequests
            {
                SeasonNumber = seasonNumber,
                Episodes = titles.Select((title, index) => new EpisodeRequests
                {
                    EpisodeNumber = index + 1,
                    Title = title
                }).ToList()
            };
        }

        private static IEnumerable<Episode> BuildEpisodes(int seasonNumber, IReadOnlyList<string> titles)
        {
            return titles.Select((title, index) => new Episode
            {
                id = (seasonNumber * 100) + index + 1,
                seasonNumber = seasonNumber,
                episodeNumber = index + 1,
                title = title
            });
        }

        private static readonly string[] DahmerTitles =
        {
            "Episode One",
            "Please Don't Go",
            "Doin' a Dahmer",
            "The Good Boy Box",
            "Blood on Their Hands",
            "Silenced",
            "Cassandra",
            "Lionel",
            "The Bogeyman",
            "God of Forgiveness, God of Vengeance"
        };

        private static readonly string[] LizzieTitles =
        {
            "Bloodbath",
            "Strong Kitty",
            "Whack Job!",
            "R.I.P (Rest in Pestilence) Abby Borden",
            "41",
            "Bed and Breakfast",
            "The Trial of the Century",
            "Carnival"
        };
    }
}
