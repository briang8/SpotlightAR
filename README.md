# Spotlight AR

**An AR karaoke app for Android.** Pick a song, place a small 3D stage on your real floor or table through the phone camera, and sing. The spotlights, the crowd and your score react to your voice in real time.

Built with Unity 6 and AR Foundation (ARCore) as a vertical slice for an AR/VR course summative.

---

## What you can do

- **Browse songs by genre** (Afrobeats, Pop, Rap, R&B, Gospel) from curated YouTube karaoke playlists, or **search all of YouTube** for a karaoke version of any song.
- **Set up**: pick one of three stages and pass a quick headphones and mic check.
- **Place the stage in AR**: scan a floor or table, tap to place, pinch to resize (tabletop, room or life size) and twist to turn.
- **Sing**: the karaoke video plays below the stage while your voice drives the lights, the crowd and a crowd-hype multiplier (x1 to x3).
- **Sing mode**: for songs with synced lyrics, the original recording plays quietly (45% by default, adjustable) as a guide while the lyrics fill in line by line as the song plays.
- **Light and dark mode**: light by default, switch with the moon button on the home screen.
- **Get results**: stars, score, best score per song, and coaching stats (how much of the song you sang, longest steady note, pitch steadiness, peak hype) with a tip on what to improve.

### The three stages

| Club Neon | Arena | Rooftop |
|---|---|---|
| Crossing lasers and a disco ball that spins faster on long notes | Lighting truss, speaker stacks, sparks at score milestones and a big crowd | City skyline, string lights that glow as you sing, fireflies on long notes |

---

## Requirements

- **Unity 6000.3.27f1 (Unity 6.3 LTS)** with the **Android Build Support** module
- An **ARCore-supported Android phone** (Android 8.0 / API 26 or newer) with **Google Play Services for AR** installed
- **Headphones** (wired or Bluetooth) for singing: the app checks that the song is not playing through a speaker, so the mic only hears you
- A **YouTube Data API v3** key (free). Song tempo and key come from open databases that need no key

## How to run it

1. **Clone the repo** and open the folder in Unity Hub with Unity 6000.3.27f1.
2. **Add your YouTube API key.** It is kept out of git on purpose. Create this file and paste the key into it (nothing else in the file):
   - `Assets/SpotlightAR/Resources/Secrets/youtube_api_key.txt`
3. Open the scene **`Assets/SpotlightAR/Scenes/Main.unity`**.
4. **Test in the Editor**: press Play. AR runs in Unity's XR Simulation environment (hold right-click and use WASD to look around, left-click a surface to place the stage). Song playback is simulated in the Editor; the real YouTube video only plays on the phone. Press **End** to finish a song early.
5. **Build to a phone**: enable USB debugging on the phone, plug it in, then **File → Build Profiles → Android → Build And Run**.

### Known limitations

- **Needs an internet connection.** Songs stream from YouTube and can't be saved offline (YouTube's terms don't allow downloading or caching videos). Song lists, covers and tempo/key data are cached, so menus still load offline, but a song can't play without a connection.

- **Song key data is incomplete.** The key comes from AcousticBrainz, which stopped collecting in 2022 and detects keys automatically. A key is only used when most matching recordings agree on the notes, so newer songs (and many Afrobeats tracks) have no key. The results screen then shows the key you sang in, suggests looking up the song's key on a site like Tunebat to compare, and the pitch stat becomes "stayed in one key".
- **Sing mode can't remove only the vocals.** The song plays inside YouTube's player, which the app can't process (and YouTube's terms don't allow altering it), so Sing mode lowers the whole song's volume instead. The YouTube player also has to stay visible. Lyrics are timed to the album version, so the app picks the original audio upload whose length matches the lyrics.
- **Relative keys count as the same.** F♯ major and D♯ minor use the same notes, so singing either one on that song counts as in key.
- **Some songs have no tempo** on Deezer or AcousticBrainz. Rap songs then show flow speed instead of "on the beat".

### Changing the playlists

The genre playlists live in **`Assets/SpotlightAR/Data/GenrePlaylists.asset`**. Select it in the Project window and edit the playlist IDs in the Inspector (the part after `list=` in a YouTube playlist link).

---

## Project structure

```
Assets/SpotlightAR/
├── Scripts/
│   ├── Core/        GameManager (session flow), GameState
│   ├── AR/          StagePlacer, TouchGestureInput, PlacementReticle
│   ├── Audio/       MicInput, PitchDetector, MicCheck, SpeakerCheck
│   ├── Scoring/     ScoreManager, PerformanceResult, PlayerProfile
│   ├── Songs/       SongCatalog, YouTubeService, SongCache, Playback/
│   ├── Stages/      Stage (abstract), NeonStage, ArenaStage, RooftopStage, effects
│   └── UI/          UIManager and one class per screen
├── Shaders/         Custom lightweight URP shaders (beams, lasers, glows, skyline)
├── Models/          CC0 crowd and stage gear models (glTF)
├── Prefabs/         Stage prefabs built from reusable parts
├── UI/              UI Toolkit layouts (UXML), styles (USS) and fonts
└── Scenes/Main.unity
```

## Credits

- Song tempo: **[Deezer API](https://developers.deezer.com/api)**
- Song key and backup tempo: **[AcousticBrainz](https://acousticbrainz.org)** (CC0 data), matched through **[MusicBrainz](https://musicbrainz.org)**
- Song lists and video playback: **YouTube Data API v3** and the **YouTube IFrame Player API**
- In-app web view: **[gree/unity-webview](https://github.com/gree/unity-webview)** (zlib licence)
- Synced lyrics for Sing mode: **[LRCLIB](https://lrclib.net)**
- Fonts: **[Inter and Inter Display](https://github.com/rsms/inter)** and **[Bricolage Grotesque](https://github.com/ateliertriay/bricolage)** (SIL Open Font License, licence files in `Assets/SpotlightAR/UI/Fonts`)
- Crowd characters: **[Quaternius](https://quaternius.com)** Ultimate Modular Men and Women packs (CC0)
- Speaker and microphone stand models: **iPoly3D** via **[Poly Pizza](https://poly.pizza)** (CC0)
- Model import: **[glTFast](https://docs.unity3d.com/Packages/com.unity.cloud.gltfast@latest)** (Unity)
