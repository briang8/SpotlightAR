using System;
using System.Collections.Generic;
using SpotlightAR.Songs;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpotlightAR.UI
{
    /// <summary>
    /// Home: greeting, search, genre chips, a rotating "Number 1 today" card and the
    /// "Top this week" list (screens_v2.png, left).
    /// </summary>
    public sealed class HomeScreen : UIScreen
    {
        public const string ForYou = "For you";
        const int CollapsedCount = 5;
        const int FeaturedCount = 3;
        const long FeaturedIntervalMs = 5000;

        readonly VisualTreeAsset rowTemplate;
        readonly Label greeting;
        readonly TextField search;
        readonly VisualElement chips;
        readonly VisualElement featured;
        readonly VisualElement featuredCover;
        readonly Label featuredTitle;
        readonly Label featuredMeta;
        readonly VisualElement featuredDots;
        readonly Label listTitle;
        readonly Button seeAll;
        readonly VisualElement list;
        readonly Label status;

        readonly List<Button> chipButtons = new List<Button>();
        IReadOnlyList<Song> songs = Array.Empty<Song>();
        int featuredIndex;
        bool expanded;
        bool searching;

        public event Action<string> GenreSelected;
        public event Action<string> SearchChanged;
        public event Action<Song> SongChosen;

        public HomeScreen(VisualElement root, VisualTreeAsset rowTemplate) : base(root)
        {
            this.rowTemplate = rowTemplate;
            greeting = Require<Label>("greeting");
            search = Require<TextField>("search-field");
            chips = Require<VisualElement>("genre-chips");
            featured = Require<VisualElement>("featured-card");
            featuredCover = Require<VisualElement>("featured-cover");
            featuredTitle = Require<Label>("featured-title");
            featuredMeta = Require<Label>("featured-meta");
            featuredDots = Require<VisualElement>("featured-dots");
            listTitle = Require<Label>("list-title");
            seeAll = Require<Button>("see-all");
            list = Require<VisualElement>("song-list");
            status = Require<Label>("home-status");

            search.textEdition.placeholder = "Search songs or artists";
            search.textEdition.hidePlaceholderOnFocus = true;
            search.RegisterValueChangedCallback(evt => SearchChanged?.Invoke(evt.newValue));
            seeAll.clicked += () => { expanded = !expanded; RenderList(); };
            Require<Button>("featured-sing").clicked += () => { if (FeaturedSong != null) SongChosen?.Invoke(FeaturedSong); };
            Require<Button>("nav-sing").clicked += () => { if (FeaturedSong != null) SongChosen?.Invoke(FeaturedSong); };

            Root.schedule.Execute(AdvanceFeatured).Every(FeaturedIntervalMs);
        }

        Song FeaturedSong => songs.Count > 0 ? songs[Mathf.Min(featuredIndex, songs.Count - 1)] : null;

        protected override void OnShown() => greeting.text = Greeting(DateTime.Now);

        public void SetGenres(IEnumerable<string> genres)
        {
            chips.Clear();
            chipButtons.Clear();
            AddChip(ForYou);
            foreach (string genre in genres) AddChip(genre);
            SetSelectedGenre(ForYou);
        }

        void AddChip(string genre)
        {
            var chip = new Button(() => GenreSelected?.Invoke(genre)) { text = genre, userData = genre };
            chip.AddToClassList("chip");
            chip.AddToClassList("t-medium");
            chips.Add(chip);
            chipButtons.Add(chip);
        }

        public void SetSelectedGenre(string genre)
        {
            foreach (Button chip in chipButtons) chip.EnableInClassList("chip--selected", Equals(chip.userData, genre));
        }

        public void ShowLoading()
        {
            SetStatus("Loading songs…");
            songs = Array.Empty<Song>();
            featured.style.display = DisplayStyle.None;
            RenderSkeleton();
        }

        /// <summary>Show a genre (or "For you") list. A note is shown when using the offline cache.</summary>
        public void ShowSongs(IReadOnlyList<Song> list, string note = null)
        {
            searching = false;
            songs = list ?? Array.Empty<Song>();
            featuredIndex = 0;
            expanded = false;
            listTitle.text = "Top this week";
            SetStatus(songs.Count == 0 ? "No songs in this playlist yet." : note);
            RenderFeatured();
            RenderList();
        }

        public void ShowSearchResults(string query, IReadOnlyList<Song> results)
        {
            searching = true;
            songs = results ?? Array.Empty<Song>();
            expanded = true;
            featured.style.display = DisplayStyle.None;
            listTitle.text = songs.Count == 0 ? "No matches" : $"Results for “{query}”";
            SetStatus(songs.Count == 0 ? "Try another song or artist from the playlists." : null);
            RenderList();
        }

        public void ShowError(string message)
        {
            songs = Array.Empty<Song>();
            featured.style.display = DisplayStyle.None;
            list.Clear();
            seeAll.style.display = DisplayStyle.None;
            SetStatus(message);
        }

        void SetStatus(string message)
        {
            status.text = message ?? string.Empty;
            status.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        void RenderFeatured()
        {
            bool show = !searching && songs.Count > 0;
            featured.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;

            Song song = FeaturedSong;
            featuredTitle.text = song.title;
            featuredMeta.text = string.IsNullOrEmpty(song.artist) ? $"{song.FormattedDuration} · {song.genre}" : $"{song.artist} · {song.FormattedDuration} · {song.genre}";
            featuredCover.style.backgroundImage = StyleKeyword.None;
            featuredCover.RemoveFromClassList("cover--loaded");
            CoverImages.Apply(featuredCover, song.GetCoverUrl());

            featuredDots.Clear();
            int dots = Mathf.Min(FeaturedCount, songs.Count);
            for (int i = 0; i < dots; i++)
            {
                var dot = new VisualElement();
                dot.AddToClassList("dot");
                dot.EnableInClassList("dot--active", i == featuredIndex);
                featuredDots.Add(dot);
            }
        }

        void AdvanceFeatured()
        {
            if (!IsVisible || searching || songs.Count < 2) return;
            featuredIndex = (featuredIndex + 1) % Mathf.Min(FeaturedCount, songs.Count);
            RenderFeatured();
        }

        void RenderList()
        {
            list.Clear();
            // The featured song is rank 1, so the list starts at rank 2 like the mockup.
            int start = searching ? 0 : 1;
            int available = Mathf.Max(0, songs.Count - start);
            int count = expanded ? available : Mathf.Min(CollapsedCount, available);

            for (int i = 0; i < count; i++) list.Add(CreateRow(songs[start + i], start + i + 1));

            bool canExpand = !searching && available > CollapsedCount;
            seeAll.style.display = canExpand ? DisplayStyle.Flex : DisplayStyle.None;
            seeAll.text = expanded ? "Show less" : $"See all {songs.Count}";
        }

        VisualElement CreateRow(Song song, int rank)
        {
            TemplateContainer row = rowTemplate.Instantiate();
            row.AddToClassList("song-row-host");
            row.Q<Label>("rank").text = searching ? string.Empty : rank.ToString();
            row.Q<Label>("row-title").text = song.title;
            row.Q<Label>("row-meta").text = song.GetLabel();
            row.Q<Label>("row-duration").text = song.FormattedDuration;
            CoverImages.Apply(row.Q("row-cover"), song.GetCoverUrl());
            row.RegisterCallback<ClickEvent>(_ => SongChosen?.Invoke(song));
            return row;
        }

        void RenderSkeleton()
        {
            list.Clear();
            seeAll.style.display = DisplayStyle.None;
            for (int i = 0; i < 4; i++)
            {
                var placeholder = new VisualElement();
                placeholder.AddToClassList("skeleton-row");
                list.Add(placeholder);
            }
        }

        static string Greeting(DateTime now)
        {
            string part = now.Hour switch
            {
                >= 5 and < 12 => "morning",
                >= 12 and < 17 => "afternoon",
                >= 17 and < 21 => "evening",
                _ => "night"
            };
            return $"{now.DayOfWeek} {part}";
        }
    }
}
