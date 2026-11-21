# Spotlight AR

**An AR karaoke app for Android.** Pick a song, place a small 3D stage on your real floor or table through the phone camera, and sing. The spotlights, the crowd and your score react to your voice in real time.

Built with Unity 6 and AR Foundation (ARCore) as a vertical slice for an AR/VR course summative.

---

## What you can do

- **Browse songs by genre** (Afrobeats, Pop, Rap, R&B, Gospel) from curated YouTube karaoke playlists, or **search all of YouTube** for a karaoke version of any song.
- **Set up**: pick one of three stages and pass a quick headphones and mic check.
- **First launch**: four quick cards explain the app (replay them any time with the ? button on Home), plus short hints the first time you place a stage and sing.
- **Place the stage in AR**: scan a floor or table (a faint grid shows what was found), tap to place, pinch to resize (tabletop, room or life size) and twist to turn. The stage drops in and its lights switch on one by one. If it's bigger than the floor found, the ring turns coral and **Fit to my space** sizes it to the room. Its lighting follows the room's brightness and colour, and on phones with ARCore depth, real objects in front of it hide it.
- **Sing**: the karaoke video plays below the stage while your voice drives the lights, the crowd and a crowd-hype multiplier (x1 to x3). The crowd raises their hands when you get loud, cheers at milestones and holds up phone torches at x3.
- **Sing mode**: for songs with synced lyrics, the original recording plays quietly (45% by default, adjustable) as a guide while the lyrics fill in line by line as the song plays.
- **Light and dark mode**: light by default, switch with the moon button on the home screen.
- **Live note**: hold a note and the note you're singing (like "A3") shows on screen.
- **Get the crowd's verdict**: stars, score, your best score and how the crowd took it. The stars reflect how much you sang and how hyped the crowd got. It's built for fun, not as a singing exam.
- **Library**: your stats, personal bests and every show you've done (tap one to sing it again). **Stages**: rendered previews of each stage, your best score on each, and your neon sign colour.

### The three stages

| Club Neon | Arena | Rooftop |
|---|---|---|
| Crossing lasers and a disco ball that spins faster on long notes | Lighting truss, speaker stacks, sparks at score milestones and a big crowd | City skyline, string lights that glow as you sing, fireflies on long notes |

---

## Requirements

- **Unity 6000.3.27f1 (Unity 6.3 LTS)** with the **Android Build Support** module
- An **ARCore-supported Android phone** (Android 8.0 / API 26 or newer) with **Google Play Services for AR** installed
- **Headphones** (wired or Bluetooth) for singing: the app checks that the song is not playing through a speaker, so the mic only hears you
- A **YouTube Data API v3** key (free). Synced lyrics come from LRCLIB, which needs no key

## How to run it

1. **Clone the repo** and open the folder in Unity Hub with Unity 6000.3.27f1.
2. **Add your YouTube API key.** It is kept out of git on purpose. Create this file and paste the key into it (nothing else in the file):
   - `Assets/SpotlightAR/Resources/Secrets/youtube_api_key.txt`
3. Open the scene **`Assets/SpotlightAR/Scenes/Main.unity`**.
4. **Test in the Editor**: press Play. AR runs in Unity's XR Simulation environment (hold right-click and use WASD to look around, left-click a surface to place the stage). Song playback is simulated in the Editor; the real YouTube video only plays on the phone. Press **End** to finish a song early.
5. **Build to a phone**: enable USB debugging on the phone, plug it in, then **File → Build Profiles → Android → Build And Run**.

### Known limitations

- **Needs an internet connection.** Songs stream from YouTube and can't be saved offline (YouTube's terms don't allow downloading or caching videos). Song lists, covers and lyrics are cached and the Library and Stages tabs work offline, but a song can't play without a connection. There's no legal way to bundle offline songs: free-licence songs don't come with timed lyrics.
- **Headphones are required to be scored.** With the song playing through the phone's speaker, the mic hears the song and your voice mixed together. Cancelling the song out would need a copy of its audio, and the song plays inside YouTube's player, which the app can't read. So the app checks for headphones instead of letting the song leak into your score.
- **It's for fun, not a singing judge.** The stars come from how much you sang and the crowd hype. The app doesn't judge the words, the melody, the key or the timing of your singing: a phone mic can't tell those reliably for any YouTube song, so it doesn't pretend to. The only pitch shown is the note you're holding, where the pitch detector is accurate.
- **Occlusion depends on the phone.** Real objects only hide the stage on phones that support ARCore's Depth API. On others, the stage always draws on top, so use Fit to my space in small rooms.
- **Sing mode needs synced lyrics**, which come from LRCLIB, a community database: many songs have them, not all. Without them, the song plays as a karaoke video.
- **Sing mode can't remove only the vocals.** The song plays inside YouTube's player, which the app can't process (and YouTube's terms don't allow altering it), so Sing mode lowers the whole song's volume instead. The YouTube player also has to stay visible. Lyrics are timed to the album version, so the app picks the original audio upload whose length matches the lyrics.

### Changing the playlists

The genre playlists live in **`Assets/SpotlightAR/Data/GenrePlaylists.asset`**. Select it in the Project window and edit the playlist IDs in the Inspector (the part after `list=` in a YouTube playlist link).

---

## Project structure

```
Assets/SpotlightAR/
├── Scripts/
│   ├── Core/        GameManager (session flow), GameState
│   ├── AR/          StagePlacer, TouchGestureInput, PlacementReticle
│   ├── Audio/       MicInput, PitchDetector, MicCheck, SpeakerCheck, AudioRouteWatcher
│   ├── Scoring/     ScoreManager (score, hype, stars), PerformanceResult, PlayerProfile, PerformanceHistory
│   ├── Songs/       SongCatalog, YouTubeService, SongCache, SyncedLyrics, SingModeService, Playback/
│   ├── Stages/      Stage (abstract), NeonStage, ArenaStage, RooftopStage, CrowdMember, StagePreviewRenderer, effects
│   └── UI/          UIManager, one class per screen, LibraryPage, StagesPage, custom elements (Icon, VoiceMeter)
├── Shaders/         Custom lightweight URP shaders (beams, lasers, glows, skyline)
├── Models/          CC0 crowd and stage gear models (glTF)
├── Prefabs/         Stage prefabs built from reusable parts
├── UI/              UI Toolkit layouts (UXML), styles (USS) and fonts
├── Tests/EditMode/  Pitch detection tests (synthetic voices, noise rejection, note names)
└── Scenes/Main.unity
```

## Credits

- Song lists and video playback: **YouTube Data API v3** and the **YouTube IFrame Player API**
- In-app web view: **[gree/unity-webview](https://github.com/gree/unity-webview)** (zlib licence)
- Synced lyrics for Sing mode: **[LRCLIB](https://lrclib.net)**
- Fonts: **[Inter and Inter Display](https://github.com/rsms/inter)** and **[Bricolage Grotesque](https://github.com/ateliertriay/bricolage)** (SIL Open Font License, licence files in `Assets/SpotlightAR/UI/Fonts`)
- Crowd characters: **[Quaternius](https://quaternius.com)** Ultimate Modular Men and Women packs (CC0)
- Speaker and microphone stand models: **iPoly3D** via **[Poly Pizza](https://poly.pizza)** (CC0)
- Model import: **[glTFast](https://docs.unity3d.com/Packages/com.unity.cloud.gltfast@latest)** (Unity)
