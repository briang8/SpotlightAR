# Spotlight AR

**An AR karaoke app for Android.** Pick a song, place a small 3D stage on your real floor or table through the phone camera, and sing. The spotlights, the crowd and your score react to your voice in real time.

Built with Unity 6 and AR Foundation (ARCore) as a vertical slice for an AR/VR course summative.

---

## What you can do

- **Browse songs by genre** (Afrobeats, Pop, Rap, R&B, Gospel) from curated YouTube karaoke playlists, or **search all of YouTube** for a karaoke version of any song.
- **Set up**: pick one of three stages and pass a quick headphones and mic check.
- **Place the stage in AR**: scan a floor or table, tap to place, pinch to resize (tabletop, room or life size) and twist to turn.
- **Sing**: the karaoke video plays above the stage while your voice drives the lights, the crowd and a crowd-hype multiplier (x1 to x3).
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
- A **YouTube Data API v3** key (free) and a **GetSongBPM** API key (free)

## How to run it

1. **Clone the repo** and open the folder in Unity Hub with Unity 6000.3.27f1.
2. **Add your API keys.** They are kept out of git on purpose. Create these two files and paste one key into each (nothing else in the file):
   - `Assets/SpotlightAR/Resources/Secrets/youtube_api_key.txt`
   - `Assets/SpotlightAR/Resources/Secrets/getsongbpm_api_key.txt`
3. Open the scene **`Assets/SpotlightAR/Scenes/Main.unity`**.
4. **Test in the Editor**: press Play. AR runs in Unity's XR Simulation environment (hold right-click and use WASD to look around, left-click a surface to place the stage). Song playback is simulated in the Editor; the real YouTube video only plays on the phone. Press **End** to finish a song early.
5. **Build to a phone**: enable USB debugging on the phone, plug it in, then **File → Build Profiles → Android → Build And Run**.

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
├── Prefabs/         Stage prefabs built from reusable parts
├── UI/              UI Toolkit layouts (UXML), styles (USS) and fonts
└── Scenes/Main.unity
```

## Credits

- Song tempo and key data: **[GetSongBPM](https://getsongbpm.com)**
- Song lists and video playback: **YouTube Data API v3** and the **YouTube IFrame Player API**
- In-app web view: **[gree/unity-webview](https://github.com/gree/unity-webview)** (zlib licence)
- Fonts: **[Inter](https://github.com/rsms/inter)** and **[Bricolage Grotesque](https://github.com/ateliertriay/bricolage)** (SIL Open Font License, licence files in `Assets/SpotlightAR/UI/Fonts`)
