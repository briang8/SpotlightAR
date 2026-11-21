using System;
using System.Collections.Generic;
using SpotlightAR.Songs;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpotlightAR.UI
{
    public enum HomeTab { Songs, Library, Stages }

    /// <summary>
    /// Home with three tabs. Songs: greeting, search, genre chips, a rotating featured card and
    /// the song list. Library and Stages are drawn by <see cref="LibraryPage"/> and
    /// <see cref="StagesPage"/>; the GameManager fills them when their tab opens.
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
        readonly Label featuredBadge;
        readonly Label featuredTitle;
        readonly Label featuredMeta;
        readonly VisualElement featuredDots;
        readonly Label listTitle;
        readonly Button seeAll;
        readonly VisualElement list;
        readonly Label status;
        readonly Button searchOnline;

        readonly List<Button> chipButtons = new List<Button>();
        IReadOnlyList<Song> songs = Array.Empty<Song>();
        int featuredIndex;
        bool expanded;
        bool searching;

        public event Action<string> GenreSelected;
        public event Action<string> SearchChanged;
        public event Action<Song> SongChosen;

        /// <summary>The player asked to search all of YouTube for the current query.</summary>
        public event Action<string> OnlineSearchRequested;

        /// <summary>The moon/sun button: switch between light and dark mode.</summary>
        public event Action ThemeToggleRequested;

        /// <summary>The help button: replay the tutorial cards.</summary>
        public event Action HelpRequested;

        readonly Icon themeIcon;
        readonly VisualElement[] pages = new VisualElement[3];
        readonly Button[] navItems = new Button[3];

        public LibraryPage Library { get; }
        public StagesPage Stages { get; }
        public HomeTab Tab { get; private set; } = HomeTab.Songs;

        /// <summary>The player switched tabs (Songs, Library, Stages).</summary>
        public event Action<HomeTab> TabChanged;

        /// <summary>Shows a sun in dark mode (tap for light) and a moon in light mode.</summary>
        public void SetDarkMode(bool dark) => themeIcon.Glyph = dark ? IconGlyph.Sun : IconGlyph.Moon;

        public HomeScreen(VisualElement root, VisualTreeAsset rowTemplate) : base(root)
        {
            this.rowTemplate = rowTemplate;
            greeting = Require<Label>("greeting");
            search = Require<TextField>("search-field");
            chips = Require<VisualElement>("genre-chips");
            featured = Require<VisualElement>("featured-card");
            featuredCover = Require<VisualElement>("featured-cover");
            featuredBadge = Require<Label>("featured-badge");
            featuredTitle = Require<Label>("featured-title");
            featuredMeta = Require<Label>("featured-meta");
            featuredDots = Require<VisualElement>("featured-dots");
            listTitle = Require<Label>("list-title");
            seeAll = Require<Button>("see-all");
            list = Require<VisualElement>("song-list");
            status = Require<Label>("home-status");
            searchOnline = Require<Button>("search-online");
            searchOnline.clicked += RequestOnlineSearch;
            search.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) RequestOnlineSearch();
            });
            searchOnline.style.display = DisplayStyle.None;

            search.textEdition.placeholder = "Search songs or artists";
            search.textEdition.hidePlaceholderOnFocus = true;
            search.RegisterValueChangedCallback(evt => SearchChanged?.Invoke(evt.newValue));
            seeAll.clicked += () => { expanded = !expanded; RenderList(); };
            Require<Button>("featured-sing").clicked += () => { if (FeaturedSong != null) SongChosen?.Invoke(FeaturedSong); };
            themeIcon = Require<Icon>("theme-icon");
            Require<Button>("theme-button").clicked += () => ThemeToggleRequested?.Invoke();
            Require<Button>("help-button").clicked += () => HelpRequested?.Invoke();

            pages[0] = Require<VisualElement>("page-songs");
            pages[1] = Require<VisualElement>("page-library");
            pages[2] = Require<VisualElement>("page-stages");
            navItems[0] = Require<Button>("nav-home");
            navItems[1] = Require<Button>("nav-library");
            navItems[2] = Require<Button>("nav-stages");
            for (int i = 0; i < navItems.Length; i++)
            {
                var tab = (HomeTab)i;
                navItems[i].clicked += () => SelectTab(tab);
            }
            Library = new LibraryPage(Require<VisualElement>("library-content"));
            Stages = new StagesPage(Require<VisualElement>("stages-content"));
            Library.BrowseRequested += () => SelectTab(HomeTab.Songs);
            ShowTab(HomeTab.Songs);

            Root.schedule.Execute(AdvanceFeatured).Every(FeaturedIntervalMs);
        }

        /// <summary>Switch tab from a tap: show it and let the GameManager fill it.</summary>
        void SelectTab(HomeTab tab)
        {
            ShowTab(tab);
            TabChanged?.Invoke(tab);
        }

        public void ShowTab(HomeTab tab)
        {
            Tab = tab;
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].style.display = i == (int)tab ? DisplayStyle.Flex : DisplayStyle.None;
                navItems[i].EnableInClassList("nav__item--active", i == (int)tab);
            }
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

        void RequestOnlineSearch()
        {
            string query = search.value?.Trim();
            if (!string.IsNullOrEmpty(query)) OnlineSearchRequested?.Invoke(query);
        }

        public void ShowOnlineSearching(string query)
        {
            searching = true;
            searchOnline.style.display = DisplayStyle.None;
            SetFeaturedVisible(false);
            listTitle.text = "Searching YouTube…";
            SetStatus($"Looking for karaoke versions of “{query}”");
            RenderSkeleton();
        }

        public void ShowOnlineResults(string query, IReadOnlyList<Song> results)
        {
            searching = true;
            songs = results ?? Array.Empty<Song>();
            expanded = true;
            searchOnline.style.display = DisplayStyle.None;
            SetFeaturedVisible(false);
            listTitle.text = $"YouTube: “{query}”";
            SetStatus(songs.Count == 0 ? "No karaoke versions found that can play in the app. Try the artist's name too." : null);
            RenderList();
        }

        public void ShowLoading()
        {
            searchOnline.style.display = DisplayStyle.None;
            SetStatus("Loading songs…");
            songs = Array.Empty<Song>();
            SetFeaturedVisible(false);
            RenderSkeleton();
        }

        /// <summary>Show a genre (or "For you") list. A note is shown when using the offline cache.</summary>
        public void ShowSongs(IReadOnlyList<Song> list, string note = null)
        {
            searching = false;
            searchOnline.style.display = DisplayStyle.None;
            songs = list ?? Array.Empty<Song>();
            featuredIndex = 0;
            expanded = false;
            listTitle.text = "More songs";
            SetStatus(songs.Count == 0 ? "No songs in this playlist yet." : note);
            RenderFeatured();
            RenderList();
        }

        public void ShowSearchResults(string query, IReadOnlyList<Song> results)
        {
            searching = true;
            songs = results ?? Array.Empty<Song>();
            expanded = true;
            SetFeaturedVisible(false);
            listTitle.text = songs.Count == 0 ? "Not in the playlists" : $"In the playlists: “{query}”";
            SetStatus(songs.Count == 0 ? "Search all of YouTube for a karaoke version instead." : null);
            searchOnline.Q<Label>("search-online-label").text = $"Search YouTube for “{query}”";
            searchOnline.style.display = DisplayStyle.Flex;
            RenderList();
        }

        public void ShowError(string message)
        {
            searchOnline.style.display = DisplayStyle.None;
            songs = Array.Empty<Song>();
            SetFeaturedVisible(false);
            list.Clear();
            seeAll.style.display = DisplayStyle.None;
            SetStatus(message);
        }

        void SetStatus(string message)
        {
            status.text = message ?? string.Empty;
            status.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>The featured card and its carousel dots always show and hide together.</summary>
        void SetFeaturedVisible(bool visible)
        {
            DisplayStyle display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            featured.style.display = display;
            featuredDots.style.display = display;
        }

        void RenderFeatured()
        {
            bool show = !searching && songs.Count > 0;
            SetFeaturedVisible(show);
            if (!show) return;

            Song song = FeaturedSong;
            featuredBadge.text = PickBadge(DateTime.Now);
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
            if (!IsVisible || Tab != HomeTab.Songs || searching || songs.Count < 2) return;
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

        static string PartOfDay(DateTime now) => now.Hour switch
        {
            >= 5 and < 12 => "morning",
            >= 12 and < 17 => "afternoon",
            >= 17 and < 21 => "evening",
            _ => "night"
        };

        static string Greeting(DateTime now) => $"{now.DayOfWeek} {PartOfDay(now)}";

        /// <summary>"THIS MORNING'S PICK" ... "TONIGHT'S PICK", from the phone's clock.</summary>
        static string PickBadge(DateTime now)
        {
            string part = PartOfDay(now);
            return part == "night" ? "TONIGHT'S PICK" : $"THIS {part.ToUpperInvariant()}'S PICK";
        }
    }
}
