# Decisions

Format: date · decision · reason · alternatives considered.

## 2026-10-05 · M0

### D1 · Unity 6000.3.25f1 LTS (the owner's installed editor)
- **Reason:** It meets HANDOVER §5.2 (6000.3.x LTS). Android Build Support, OpenJDK 17.0.18, NDK r27c and SDK platforms 34–37 are all installed through the Hub.
- **Alternatives:** none needed.

### D2 · minSdk 25, not 23
- **Reason:** HANDOVER §5.3 says minSdk must equal Unity's minimum API level. For Unity 6000.3 that minimum is 25 (the configurator picks the lowest level the editor offers that is ≥ 23, and that resolves to 25). The plugin's example app also uses 25.
- **Alternatives:** 23. Unity 6000.3 can't build for it.

### D3 · Android toolchain: AGP 9.1.0 + Gradle 9.3.1 (Flutter 3.47 defaults), NDK r27c, JDK 17
- **Reason:** HANDOVER §5.3 lists AGP 8.10 and Gradle 8.13, but the exported `unityLibrary/gradle.properties` from Unity 6000.3.25 says `unity.agpVersion=9.0.0`, so the table is out of date. The plugin's own example app (main branch, Aug 2026) builds `unityLibrary` with AGP 9.1.0 and Gradle 9.3.1. `android.newDsl=false` and `android.builtInKotlin=false` stay set, as both templates do.
- **JDK:** Gradle and Flutter use Unity's bundled Temurin 17 (`flutter config --jdk-dir`). The system Java is 21, and nothing extra was installed.
- **Fallback:** if a later Unity export breaks with AGP 9, pin AGP 8.10.0 and Gradle 8.13.

### D4 · Embedding plugin: flutter_embed_unity 2.0.0 + flutter_embed_unity_6000_0_android 2.0.0 (pinned)
- **Reason:** These are the latest stable versions, and they explicitly support 6000.0/6000.3 and batchmode export. 2.1.0-dev.1 is a prerelease, so it isn't used. The Unity-side scripts are vendored from the repo's 6000 example project (identical to the `.unitypackage`) so the setup can be reproduced without the Editor UI.
- **Alternatives:** flutter_unity_widget_2. That is the fallback if the spike fails.

### D5 · Unity project created by `-createProject`, with URP added via the manifest and configured by code
- **Reason:** Everything is reproducible from the command line. `AndroidProjectConfigurator.ApplyAll()` runs before every build, so settings can't drift. It sets IL2CPP, ARMv7+ARM64 (+x86-64 for emulator builds only), OpenGLES3 only, portrait, Input "Both", Application Entry = Activity, targetSdk 36, and a mobile URP asset with no realtime shadows, HDR or MSAA.
- **Alternatives:** asking the owner to create it in Unity Hub. That wasn't needed.

### D6 · Bridge JSON on Unity via Newtonsoft (`com.unity.nuget.newtonsoft-json`), using JObject only
- **Reason:** `JsonUtility` can't represent the free-form `payload` object. Newtonsoft is Unity's own package and uses the invariant culture. Using `JObject` (with no reflection-based deserialization) avoids IL2CPP stripping problems.

### D7 · Game-specific settings go through an `IProjectSetup` interface (Core finds the one implementation)
- **Reason:** The factory rule (§5.4). `BuildScripts` and `AndroidProjectConfigurator` live in `Assets/Core/Editor`, and Game #2 only provides its own `IProjectSetup` (IDs, scenes).

### D8 · One app-wide `UnityBridge` in Flutter
- **Reason:** Unity is loaded once and can't be unloaded (§5.2), so `unity.ready` arrives only once per process. The bridge's ready state must outlive any single screen.

### D9 · New AVDs `ur_api36` (API 36 x86_64 Google APIs, 4 GB, host GPU) and `ur_lowspec` (API 36, 2 GB, 720×1280)
- **Reason:** The existing `caravan_test` AVD has its GPU disabled and runs API 34, so it doesn't meet §8.1. It was left untouched.

### D10 · M0 spike verdict: the combined app **is testable on the emulator** (x86-64 build)
Run on 2026-10-05, AVD `ur_api36` (API 36, x86_64, host GPU via Mesa Intel, GLES 3.0), release APK with ARMv7 + ARM64 + x86-64. Script: `tools/spike_m0.sh`. Raw results: `release/logs/m0_spike_results.txt`. Screenshots: `release/screenshots/m0/`.

| # | Check | Result |
| --- | --- | --- |
| 1 | Unity renders | PASS. The cube renders inside the Flutter screen. Unity cold load is about 3.4 s (`libmain` load → `unity.ready`). |
| 2 | Touch + swipe | PASS. 4/4 adb swipes (L, R, U, D) were detected by Unity inside the embed. |
| 3 | ping→pong < 100 ms | PASS. Median 16 ms, max 34 ms (n=20, release build). |
| 4 | Pause / resume | PASS, both the bridge `game.pause`/`game.resume` and the plugin's `pauseUnity`/`resumeUnity`. |
| 5 | Leave screen and return | PASS. Same process, and the bridge stays alive without a new `unity.ready` (as expected, see D8). |
| 6 | Background / foreground | PASS. Same process, and pings work afterwards. |
| 7 | Rotation | PASS. With system rotation forced to landscape plus `adb emu rotate`, there's no restart and the app stays portrait. |
| – | Logcat | No `FATAL EXCEPTION`, `ANR`, native crash markers or `UR_ERROR`. PSS is about 270 MB. |

- **Consequence:** owner task 6 (a physical phone) is **not** needed for M3/M6. A real phone is still welcome for representative performance numbers (§10.3 #13).
- **What made it work:** the x86-64 (Chrome OS) target architecture (emulator builds only), OpenGLES3 only, and a host-GPU AVD. The plugin README's "libmain.so not found" emulator error comes from building without x86-64.

### D11 · Unity batchmode isolated from the emulator's adb, plus a shutdown watchdog
- **Problem:** With an emulator running, Unity batchmode killed the shared adb server on exit. Unity's bundled adb 36.0.0 and the SDK's adb 37.0.1 clash on port 5037, and the emulator dropped off. Separately, batchmode often hangs during shutdown after reporting its result (KNOWN_ISSUES K1).
- **Decisions:** (1) Unity editor preference `AndroidADBKillServerOnExit` = 0 on this machine (backup of the previous prefs in `build/unity-prefs.backup`). (2) `ur_unity` runs Unity with `ANDROID_ADB_SERVER_PORT=5099`, so Unity's adb never touches the emulator's server. (3) `ur_unity` reads Unity's own verdict from the log (`Exiting with code N`, or `UR_BATCH_EXIT N` from `BuildScripts`), stops a process that is still alive 90 s later, and returns that verdict. It also refuses to start while another Unity process has the project open.
- **Alternatives:** debugging the hang itself (no symbols, ptrace restricted), or never running Unity while an emulator is up (too fragile for M1+).

## 2026-10-05 · M1

### D12 · Gameplay is a pure C# simulation; MonoBehaviours only render it
- **Reason:** It's deterministic (seed + inputs → the same run), testable in EditMode at thousands of simulated minutes per second, and allocation-free. `RunSimulation` owns the player, collisions, pickups, power-ups, stats and revive. `RunSession` owns the bridge protocol. `TrackView`, `PlayerView` and `Hud` only read from them. Collisions are lane/AABB logic with fixed 1/120 s sub-steps, so there's no tunnelling at 30 m/s and no physics engine cost.
- **Alternatives:** Unity physics with triggers. That's non-deterministic and harder to test.

### D13 · Gaps are placed in time, not metres
- **Reason:** §3.4 requires gap ≥ speed × 0.7 s, but speed keeps rising between generation and arrival. The generator uses the exact closed-form distance↔time relation of the speed curve (`DifficultyCurve.DistanceAt/TimeAt`), so every gap takes at least `gapBeats × 0.8 s` to cross, whenever the player gets there. Pauses and the revive prompt freeze both time and distance, so the relation always holds.
- **Rolling carts** reach the row's start exactly when the player does. They're drawn rolling toward the player, so for collision purposes they act like a lane blocker at the row. This keeps the guarantee exact.

### D14 · Chunk templates are hand-authored as compact patterns and stored as ScriptableObjects
- **Reason:** §3.4 asks for ScriptableObject templates. `ChunkCatalog` holds 25 patterns (9 tier-1, 9 tier-2, 7 tier-3), turned into assets once. After that the assets are the source of truth and can be edited in the Inspector, and `ChunkLibrary.Validate()` plus the generator tests catch authoring mistakes.

### D15 · In-run HUD uses TextMeshPro (from the ugui 2.0 package)
- **Reason:** `SetText("{0} m", value)` doesn't allocate, which matters for the 0 B/frame budget. The TMP Essential Resources are committed, because batchmode can't import a .unitypackage synchronously (`tools/lib/extract_unitypackage.py`). The EmojiOne sample sprites that ship with them were removed because their license terms aren't clear (rule 5).

### D16 · Visuals are built from primitives and pooled at scene load
- **Reason:** §3.11 (no Instantiate during a run). `TrackView` maps sim item ids to fixed slots. `ObjectPool.GrowCount` must stay 0, and a PlayMode test asserts this after a 3-minute run. M2 replaces the primitives with the Lantern City art pass.

### D17 · M0 spike retired
- The spike scene, `SpikeController` and its PlayMode tests were replaced by the Run scene and `RunSceneTests` (unity.ready, debug.ping, pause and resume are covered there). The Flutter spike screen stays until M3 rebuilds the shell; `tools/spike_m0.sh` is kept as the record of the M0 verdict.

## 2026-10-05/06 · M2 + M3

### D18 · Look: stylised toon shading on CC0 models, not photo-real
- **Reason:** The owner asked for a "premium semi-real" look and approved the v2 mockups. On mobile GPUs and the emulator, a custom toon shader (`ToonLit`, plus `ToonLitOutline` with an inverted-hull outline only on characters and obstacles), additive glows and quarter-resolution bloom give that look at a fraction of PBR's cost. The courier is the Kenney CC0 character with our own skins (`SkinBaker`), animated with Playables (run/jump/idle; slide is procedural). The street uses Kenney CC0 city kits re-shaded with the same material.
- **Alternatives:** URP Lit with real-time shadows (16–20 FPS on the emulator in the first art pass), and paid asset-store packs (cost money, and licences need owner approval).

### D19 · Street segments merged into one mesh per material; sparks GPU-instanced
- **Problem:** After the art pass the emulator ran at 38–49 FPS, but with 290–390 draw calls (budget ~100). `StaticBatchingUtility` on moving segment roots still issued roughly one draw per part on GLES.
- **Decision:** `Core/Rendering/MeshMerge.CombineByMaterial` merges each 26 m segment at load into a single renderer with one sub-mesh per material; line-renderer cords stay separate. Sparks are no longer GameObjects: `TrackView` draws every visible spark and its halo with `Graphics.RenderMeshInstanced` (2 draws, preallocated matrices, 0 B/frame). Tests: `MeshMergeTests`, plus the PlayMode autoplay test, which checks that sparks were drawn and pools never grew.
- **Other perf settings:** renderScale 0.8, bloom at quarter resolution, camera near clip 2.5, no real-time shadows (blob shadows instead).

### D20 · Fonts and icons bundled, the same on both sides
- Bricolage Grotesque ExtraBold (display) and Instrument Sans (UI) are used in both Flutter and the Unity HUD (TMP SDF assets). Icons come from the Phosphor icon font. Nothing is fetched at runtime.

### D21 · `phosphor_flutter` replaced by a generated icon file
- **Reason:** The package doesn't compile with Flutter 3.47 (it subclasses `IconData`, which is now final). `lib/core/widgets/phosphor.dart` has const `IconData`s for the glyphs we use, on the bundled `Phosphor-Fill`/`Phosphor-Bold` fonts, and keeps the package's call style (`PhosphorIcons.x(PhosphorIconsStyle.fill)`).

### D22 · Bridge v2
- `session.start.bestDistance` lets Unity draw the "best" line and the "new best" moment. `session.ended.tutorialCompleted` tells Flutter whether every step was finished (analytics). Flutter marks the tutorial done after the first tutorial run either way, so nobody is forced through it twice. See `docs/bridge-contract.md`.

### D23 · Local-first economy in Flutter until Firebase is connected
- **Reason:** §4.1 asks for no login wall and full offline play. `GameState` keeps the profile in shared_preferences, applies each `sessionId` once (with a bounded list of processed IDs), caps sparks at 1.2 per metre (logging `result_rejected`), and evaluates missions from `stats`. M4 syncs this document to Firestore with merge rules. Ads (`DebugAds`) and the store (`FakeStore`) are interface-backed fakes until M5, so every flow is testable without network or accounts.

### D24 · Flutter's root screen switch lives inside a stable home widget
- **Problem (bug):** The app hung on the splash, because changing `MaterialApp.home` doesn't replace the Navigator's first route.
- **Decision:** `_RootGate` (a fixed `home`) picks splash, onboarding or shell from an inherited `_BootScope`. Regression: `test/flows/app_flow_test.dart` (fresh install → onboarding).

## 2026-10-06 · M4

### D25 · Cloud sync is a reusable Core engine with a per-game adapter
- `lib/core/sync/cloud_sync.dart` (`CloudSync<T>`, `CloudBackend`, `SyncAdapter<T>`) does the work: sign in, read from the server, merge, write back. It debounces saves (2 s), retries every 30 s when offline, and never blocks play or the splash. Game #2 only writes an adapter.
- `UrbanRunnerSyncAdapter` maps the profile onto the §7.1 layout (`users/{uid}` + `games/urban_runner/players/{uid}`). Device-only fields stay local: processed session IDs, ad pacing, and Remove Ads (Play restores it on every start).
- **Rules-aware uploads:** the rules allow sparks to rise by at most 20,000 per write, so a larger offline gain is uploaded in steps across consecutive syncs. The device balance itself is never cut.
- Reads use `Source.server`, because merging against Firestore's offline cache could undo another device's progress.
- **Tests:** `test/games/profile_sync_test.dart` runs against an in-memory backend that enforces the same checks as `firebase/firestore.rules`. `firebase/tests/rules.test.mjs` tests the real rules in the Emulator Suite.

### D26 · Firebase switches itself on
- `FirebaseSetup.init()` picks one of three modes and never throws. (1) With `google-services.json` at build time, the Gradle plugins are applied and the real project is used (Auth, Firestore, Analytics, Crashlytics, Remote Config). (2) A debug build with `--dart-define=UR_FIREBASE_EMULATOR=10.0.2.2` uses the Emulator Suite under project `demo-urban-runner`. (3) Otherwise Firebase is off and the app is local-only with debug sinks. No secrets or config files are committed.

## 2026-10-06 · In-game redesign (owner: "decide yourself; humanised, very colourful, unique")

### D27 · Look: "Lantern Festival at dusk"
- **Problem:** The courier was 3.92 m tall (the model's units were never normalised), so they filled a third of the screen. The street was a single purple tone, and obstacles were plain boxes.
- **Decisions:**
  - The courier is scaled to 1.75 m from measured world bounds (`PlayerView.CourierHeight`).
  - High chase camera (5.4 m up, 7.9 m back, FOV 60, near clip 0.6). It follows lanes at 0.8 and rises with roofs but not with jumps.
  - A warm three-stop gradient sky with a low sun (`SkyGradient` shader), and a painted colour for every building (7 tints over the Kenney colormap).
  - Terracotta pavements, tram rails instead of painted lane dashes, and lanterns raised above the camera.
  - Bloom only on HDR emissives, so the bright sky doesn't haze.
  - HUD text uses font materials with a soft underlay, so it reads on the light sky.
- **Tool:** `tools/ingame_shots.sh` renders phone-sized frames from the real Run scene (an explicit PlayMode test) for design review without an emulator build.

### D28 · Gameplay: the chase, trams and feel
- **The Warden** (an original patrol drone) is the reason to run. It hunts the courier for the first 3 s of every run. A side bump (moving into a lane beside an obstacle you're already alongside) bounces you back and brings the drone in with a searchlight. A second bump within 8 s means caught. Head-on hits are still crashes.
- **Trams:** `Tram` (blocks at street level, 10 m) and `TramRamp` (a 4 m boarding ramp, then the roof). Roofs are walkable at 2.4 m, you can cross between adjacent roofs, you fall off the end, and roofs carry a spark line.
- **Movement:** gravity jump (0.8 s, 1.9 m), lane changes allowed in mid-air, and a swipe down in the air slams you down and slides on landing. The fairness test was updated to match: being in the air matters only when the next safe lane needs another jump, and the generator adds a beat in that case.
- **Speed you can feel:** "Faster!" moments at 10/12.5/15/17.5/20/22 m/s, wind streaks from 11 m/s, music up to +8% pitch, and the FOV widens.
- **Sparks** are spinning diamond gems drawn with GPU instancing.
- **Bridge:** unchanged (caught runs report `cause: obstacle`), so `v` stays 2.

### D30 · Flutter owns the Back gesture
- **Problem (negative test N1):** On Android 16 with targetSdk 36, Back is delivered through `OnBackInvokedCallback`. With Unity's predictive-back support on, the embedded Unity view registered last and swallowed the gesture, so the run never paused.
- **Decision:** Unity's `predictiveBackSupport` is off (set by `AndroidProjectConfigurator`, with a regression test in `ProjectSetupTests`). Flutter receives Back and sends `game.pause`, as the bridge contract already specified. If Back still reaches Unity as Escape, `RunSession.Back()` only pauses or declines the revive and never resumes, so two deliveries can't cancel each other out.

### D31 · Performance on real phones
The owner reported in-game lag on a real phone. Research and fixes:
- **Embedding mode (main cause):** the plugin's `AndroidView` shows Unity's SurfaceView through a virtual display. Every frame is drawn off-screen, copied into a texture and drawn again by Flutter, paced by Flutter. `UnityGameView` (in `lib/core/unity/`) uses Hybrid Composition (`initExpensiveAndroidView`), so SurfaceFlinger shows Unity's surface directly. Hybrid Composition slows Flutter's own UI, but Flutter draws nothing over the game during a run.
- **Adaptive quality** (`Core/Perf/AdaptiveQuality`): a five-step ladder (render scale 0.85 → 0.55, then post-processing off, then a steady 30 fps). It steps down after one 2 s window under 86 % of target and steps up after 8 s at 97 % or more. The level is remembered between sessions (never starting on the 30 fps step), and there's a pixel budget for 1440p-class screens. Low-memory phones start at step 2. Unit tests are in `AdaptiveQualityTests`.
- **Bloom removed:** with HDR off, nothing ever exceeded the bloom threshold, yet the bloom chain cost several full-screen passes. The glow comes from the additive halo sprites.
- **Shader warm-up while loading:** one off-screen frame with every obstacle and power-up visible, so drivers compile shaders before the run rather than on first sight.
- **Optimized Frame Pacing (Swappy) on, ASTC textures.** Unity has reported issues where OFP with `targetFrameRate` locks some devices to 30 fps, so the device logs (`UR_PERF ... quality_level=, fps_avg=`) are checked on real hardware.
- **Graphics API:** OpenGL ES 3 stays. Vulkan is reported to give black screens in some embedded setups; it's an experiment for real-device testing only.

## 2026-10-06 · Attention and retention (owner: "capture people's attention", "I want retention")

### D32 · Juice, visible trails, daily streak, Lantern Hunt
Based on research into daily hunts and streaks (Subway Surfers' Word Hunt), first-session guidance (players should reach the core gameplay within a minute; better onboarding has lifted retention by up to 50%) and "game juice" (hit-stop, particles, squash and stretch, rising feedback):
- **Trails show in the run** (`TrailView`). They were sold in the Locker and the Starter Pack but never drawn. Neon Ribbon is a vertex-coloured ribbon glow, Paper Kite is particles of toon-shaded paper diamonds, and Lantern is rising amber orbs plus a soft ribbon. They're cleared on floating-origin shifts.
- **Juice:**
  - Gem combo: a ×N badge from 5 gems and a pop every 5th; the pickup sound rises in pitch.
  - The sparks counter bounces on each pickup; landing gives a dust puff and a squash.
  - **"Close!"**: a lane change away from a blocking obstacle in the last 0.3 s gives 0.16 s of slow motion (`SimEvents.NearMiss`, `MovementTests`). Off with Reduce motion.
  - Every effect is allocation-free, and the PlayMode zero-alloc test still passes.
- **Daily login streak** (`streak.dart`, `StreakSheet`):
  - Rewards are 100/150/200/300/400/600/1,500 over 7 days, then the cycle repeats. Missing a UTC day resets it.
  - It is offered once per app session, on Home, never before the tutorial is done.
  - It pays once per day even with double taps or a restart, and the Home header shows a flame with the count.
- **Lantern Hunt** (bridge v3, `hunt.dart`, `HuntLanternView`):
  - Five lanterns a day, one per run, with a beam of light so it's visible from far away.
  - Completing all five pays 500 → 2,500 sparks, growing with consecutive completed days.
  - A Home card shows today's lanterns, and Results shows "Lantern found · 3 of 5".
  - The lantern uses a separate random stream, so tracks never change (proved by a test).

### D33 · No Unity splash, a branded loading cover
On a real phone the owner saw Unity's "Made with Unity" splash with our spinner drawn on top of it. Since Unity 6, Personal licences may turn the splash off, so `PlayerSettings.SplashScreen.show = false` (with a regression test). The game screen now shows a full-screen Lantern City cover ("Heading to Lantern City…", the courier and a rotating gameplay tip) from the tap on Play until Unity's `session.started`. It then fades and is removed from the tree, so no invisible animation keeps running. Flow test: `loading: a branded cover…`.
- **Follow-up (D31/D33):** the Hybrid Composition kick in `MainActivity` must only give Unity focus (`requestFocus` + `windowFocusChanged(true)`). Calling `pause()`/`resume()` while Unity is still starting blocked for 2 s and left the engine stuck, showing Unity's "Not enough storage space" dialog (KNOWN_ISSUES I5). It only happened with normal animation timing, which is why the earlier emulator check (animations off) passed. It is now verified with 4 cold starts, animations on and off. The loading cover shows the player's selected courier in full colour.


### D34 · Firebase project `urban-runner-5c944`
The owner created the project on 2026-10-10. Claude set it up in the console, and the owner accepted the Firebase and Analytics terms themselves.
- **Plan:** Spark (free). Firestore backups would need Blaze, so they stay off.
- **Analytics:** turned on.
- **Android app:** `com.gamefactory.urbanrunner`.
- **Sign-in:** Anonymous only. Auto clean-up stays off, because it would delete guest progress after 30 days.
- **Firestore:** Standard edition, production mode, location `nam5` (US multi-region). The audience is worldwide, and the game is local-first with background sync, so server distance doesn't affect gameplay. The location is permanent.
- **Rules:** `firebase/firestore.rules` was published from the console. Tests and emulators still use `demo-urban-runner` (default alias); the real project is the `prod` alias in `.firebaserc`.
- **Config file:** `google-services.json` is in `app/android/app/` and is git-ignored.
- **Not done yet:**
  - Crashlytics turns on with the first report from the SDK.
  - Debug and release SHA-1 fingerprints are only needed later, for Google or Play Games sign-in.
- **M4 wiring:**
  - `main.dart` calls `FirebaseSetup.init()`, which never throws.
  - With the real project, the app uses Firebase Analytics, Firestore cloud saves and Remote Config. Without `google-services.json`, or if init fails, it uses the local-only debug services. This is the same code path the tests use.
  - Gradle applies the google-services and Crashlytics plugins only when the file exists, so a fresh clone still builds.
  - The player document now also syncs the streak and hunt fields: `streak`, `streakBest`, `lastStreakClaim`, `huntDate`, `huntFound`, `huntStreak`, `huntLastComplete`. The rules validate them, with a new rules test and a cross-device sync test.

### D35 · Ads and purchases (M5) with Google test IDs
- **Services:** `main.dart` now uses `AdMobAdsService` (google_mobile_ads, UMP consent) and `PlayStore` (in_app_purchase). Tests keep `DebugAdsService` and `FakeStore`.
- **Test IDs only:**
  - The ad units are Google's official test units, unless a release build passes `--dart-define=UR_ADMOB_REWARDED/UR_ADMOB_INTERSTITIAL`.
  - The AdMob app ID is Google's test ID through a manifest placeholder. Only a release build with `-PurAdmobAppId=…` replaces it (owner task 5).
- **Consent:** the UMP consent check runs once per app start and is shared by onboarding and `init()`, and ads are requested only after it allows them. Settings shows "Privacy options" when UMP says it's required.
- **Bug fix (regression flow test):** `showInterstitial()` now returns whether an ad was actually shown. Before, Results counted an interstitial as shown even when none had loaded (no fill, offline), which started the 120 s / 3-run cool-down for an ad nobody saw.
- **Purchases:** until `remove_ads` and `starter_pack` exist in Play Console (owner task 6), Play returns no products and the Store screen shows them as unavailable. A real purchase test needs an internal testing track.
- **Found on the emulator, fixed with regressions:**
  - **Release crash at launch.** google_mobile_ads brings in WorkManager, whose Room database is opened through the generated `WorkDatabase_Impl()` constructor by reflection. R8 full mode (AGP 9) removed that constructor, so every launch crashed with "Failed to create an instance of androidx.work.impl.WorkDatabase".
    - **Fix:** keep rules in `app/android/app/proguard-rules.pro`.
    - **Regression:** `tools/screen_tour.sh` now fails as soon as the app process is gone. Before, it kept photographing the launcher, because its "in game" check only looked for the missing Missions tab.
  - **Ad-revive in the first session.** Unity's revive card is "Watch a short ad", and it was offered in the tutorial run and the rest of the first session. §7.4 allows no ads there.
    - **Fix:** `session.start` now sends `revive_enabled: false` for the tutorial and while `appSessions <= 1`, with no Unity change.
    - **Regressions:** a unit test, plus a flow test in which a crash during the first session asks for no ad. The fake Unity respects the switch like the real one.
