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
