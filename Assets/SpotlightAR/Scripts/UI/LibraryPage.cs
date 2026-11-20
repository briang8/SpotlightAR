using System;
using System.Linq;
using SpotlightAR.Scoring;
using SpotlightAR.Songs;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpotlightAR.UI
{
    /// <summary>
    /// The Library tab: the player's stats, their personal bests and every show they've done,
    /// newest first. Tapping a show starts that song again.
    /// </summary>
    public sealed class LibraryPage
    {
        const int BestsShown = 3;
        const int RecentShown = 30;

        readonly VisualElement content;

        public event Action<Song> SongChosen;
        /// <summary>The empty state's button: go and pick a first song.</summary>
        public event Action BrowseRequested;

        public LibraryPage(VisualElement content) => this.content = content;

        public void Show(PerformanceHistory history)
        {
            content.Clear();
            if (history == null || history.ShowCount == 0)
            {
                content.Add(EmptyState());
                return;
            }

            var stats = new VisualElement();
            stats.AddToClassList("lib-stats");
            stats.Add(Stat(history.ShowCount.ToString(), history.ShowCount == 1 ? "show" : "shows"));
            stats.Add(Stat(history.TotalStars.ToString(), "stars earned"));
            stats.Add(Stat(history.BestScore.ToString("N0"), "best score"));
            content.Add(stats);

            content.Add(Section("Personal bests"));
            int rank = 1;
            foreach (PerformanceHistory.Show show in history.PersonalBests(BestsShown)) content.Add(Row(show, rank++));

            content.Add(Section("Recent shows"));
            DateTime now = DateTime.Now;
            foreach (PerformanceHistory.Show show in history.Shows.Take(RecentShown)) content.Add(Row(show, 0, now));

            var minutes = new Label($"{history.MinutesSung:0} minutes on stage so far");
            minutes.AddToClassList("lib-footnote");
            content.Add(minutes);
        }

        VisualElement EmptyState()
        {
            var box = new VisualElement();
            box.AddToClassList("empty");
            var badge = new VisualElement();
            badge.AddToClassList("empty__badge");
            badge.Add(new Icon { Glyph = IconGlyph.Mic });
            var title = new Label("No shows yet");
            title.AddToClassList("empty__title");
            title.AddToClassList("heading--bold");
            var body = new Label("Every song you sing lands here with its score and stars, so you can see yourself improve.");
            body.AddToClassList("empty__body");
            var button = new Button(() => BrowseRequested?.Invoke()) { text = "Pick a song" };
            button.AddToClassList("primary-button");
            box.Add(badge);
            box.Add(title);
            box.Add(body);
            box.Add(button);
            return box;
        }

        static VisualElement Stat(string value, string label)
        {
            var tile = new VisualElement();
            tile.AddToClassList("lib-stat");
            var v = new Label(value);
            v.AddToClassList("lib-stat__value");
            v.AddToClassList("heading--bold");
            var l = new Label(label);
            l.AddToClassList("lib-stat__label");
            tile.Add(v);
            tile.Add(l);
            return tile;
        }

        static Label Section(string text)
        {
            var label = new Label(text);
            label.AddToClassList("list-header__title");
            label.AddToClassList("heading--bold");
            label.AddToClassList("lib-section");
            return label;
        }

        /// <summary>A show: cover, title, where/when, and the score with its stars. Rank &gt; 0 shows a medal number.</summary>
        VisualElement Row(PerformanceHistory.Show show, int rank, DateTime now = default)
        {
            var row = new VisualElement();
            row.AddToClassList("song-row");
            row.AddToClassList("lib-row");

            if (rank > 0)
            {
                var number = new Label(rank.ToString());
                number.AddToClassList("song-row__rank");
                number.AddToClassList("heading");
                row.Add(number);
            }

            var cover = new VisualElement();
            cover.AddToClassList("song-row__cover");
            cover.AddToClassList("cover");
            CoverImages.Apply(cover, show.coverUrl);
            row.Add(cover);

            var text = new VisualElement();
            text.AddToClassList("song-row__text");
            var title = new Label(show.title);
            title.AddToClassList("song-row__title");
            title.AddToClassList("t-semibold");
            string details = rank > 0
                ? (string.IsNullOrEmpty(show.artist) ? $"on {show.stage}" : $"{show.artist} · on {show.stage}")
                : $"{show.stage} · {(show.singMode ? "Sing mode" : "Karaoke")} · {PerformanceHistory.When(show.PlayedAt, now)}";
            var meta = new Label(details);
            meta.AddToClassList("song-row__meta");
            text.Add(title);
            text.Add(meta);
            row.Add(text);

            var right = new VisualElement();
            right.AddToClassList("lib-row__score");
            var score = new Label(show.score.ToString("N0"));
            score.AddToClassList("lib-row__points");
            score.AddToClassList("t-bold");
            var stars = new VisualElement();
            stars.AddToClassList("lib-row__stars");
            for (int i = 0; i < 5; i++)
            {
                var star = new Icon { Glyph = i < show.stars ? IconGlyph.StarFilled : IconGlyph.Star };
                star.AddToClassList("lib-star");
                star.EnableInClassList("lib-star--on", i < show.stars);
                stars.Add(star);
            }
            right.Add(score);
            right.Add(stars);
            row.Add(right);

            row.RegisterCallback<ClickEvent>(_ => SongChosen?.Invoke(show.ToSong()));
            return row;
        }
    }
}
