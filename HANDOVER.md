# Urban Runner — Build Handover for Claude Code

**Version** 1.0 · 5 October 2026
**Builder** Claude Code (CLI), running on the owner's computer
**Owner's machine** Unity 6 + Android emulator already installed
**Source** "Mobile Game Factory — Master Context" (PDF). This handover turns that plan into build instructions. Where the two differ, this handover wins because it reflects later decisions.

---

## 0. How to use this document

1. Read the whole document before writing any code.
2. Work through the milestones in §9 in order. Don't start a milestone until the previous milestone's exit test passes.
3. Make reasonable calls yourself, record each one in `docs/decisions.md`, and keep going. Stop to ask the owner only for the items listed in §12.
4. At the end, hand back everything listed in §11.

---

## 1. The idea

We take classic games that players already know and love and rebuild them for today. Each one gets a clean modern UI, new features the originals never had, and a completely original world. The gameplay feels instantly familiar, and everything around it feels new.

**Formula:** familiar gameplay + modern UI + new features + original identity = a game that's easy to pick up and gives players reasons to keep coming back.

**The business:** a game factory, not a one-game studio. We release small games, measure them, and invest in the winners. This app is Game #1, and its code must make Game #2 cheaper to build (see §5.4).

**The rule:** we keep the mechanic, never the content. Nothing may come from an existing game: no characters, names, maps, art, sounds, music, levels or UI. Every third-party asset must be original or licensed, and listed in `ASSETS_LICENSES.md`.

---

## 2. Scope

### In scope (this handover)
- An Android app containing **Urban Runner** (working title), a modern endless runner.
- A Flutter app shell (menus, profile, missions, locker, store, settings) around a Unity 6 game.
- Firebase: anonymous sign-in, Firestore sync, Analytics, Crashlytics and Remote Config.
- Monetization: rewarded ads, capped interstitials, and two in-app purchases.
- Full automated and emulator testing, plus a hand-back package (§11).

### Out of scope
- iOS. It needs macOS and Xcode, so it comes in a later handover. Keep code portable anyway.
- Store submission, server-side purchase verification, and any custom backend.
- Multiplayer, leaderboards, chat and friends.
- Games #2–15, and Project Sentinel (the browser FPS).

---

## 3. Game design — Urban Runner

### 3.1 Original theme: "Lantern City"
- **Setting:** a rainy night-market city lit by paper lanterns and neon signs. The signs use invented glyphs only, never real brands or words.
- **Player:** a young courier running parcels across the city before the market closes.
- **Tone:** bright, friendly and energetic. No violence, no weapons, and no chaser character.
- **Currency:** *sparks*, small glowing orbs.
- **Characters (cosmetic only):** three original couriers named Mira, Dex and Juno. You may rename them, but the names must stay original.
- **Palette** (shared by Unity and Flutter so the menus match the game):

| Token | Hex | Use |
| --- | --- | --- |
| night | `#0E1230` | backgrounds |
| surface | `#1A2050` | cards, panels |
| neon-magenta | `#FF3DA6` | primary accent, Play button |
| neon-cyan | `#2EE6FF` | sparks, highlights |
| lantern-amber | `#FFB547` | obstacles, warnings |
| text | `#F4F6FF` | text on dark |
| success | `#3DFFA2` | rewards, new best |
| danger | `#FF5A5A` | errors, crash |

Obstacles must be readable by **shape**, not only by colour, so that colour-blind players can play.

### 3.2 Core loop
Start → run → collect sparks → dodge obstacles → use power-ups → speed rises → crash → revive (optional) → results and rewards → upgrade and unlock → run again.

### 3.3 Controls
- Portrait orientation with three lanes.
- Swipe left/right to change lane, swipe up to jump, swipe down to slide.
- Keyboard arrow keys in the Editor for testing.
- Input buffer: 0.15 s, so a swipe made slightly early still counts. Lane change duration: about 0.12 s.
- Swipe detection must work inside the Flutter embed (verify this in the M0 spike).

### 3.4 Track, obstacles, pickups

| Item | Player action | Notes |
| --- | --- | --- |
| Low crate stack | Jump | tier 1+ |
| Hanging banner / sign | Slide | tier 1+ |
| Market stall / parked van | Change lane | tier 1+ |
| Rolling cart moving toward player | Change lane | tier 3+ |
| Spark lines | Collect | straight, arcs over jumps |

- The track is built from hand-authored **chunk templates** (ScriptableObjects), grouped by difficulty tier and chosen with a **seeded RNG**, so any run can be reproduced exactly.
- **Guarantee:** every row has at least one passable lane, and the player always has time to reach it. The gap between two required actions must be at least `speed × 0.7 s`. A test must prove this (§10).

### 3.5 Power-ups (one spawns every 250–400 m)

| Power-up | Effect | Base duration | Upgrade |
| --- | --- | --- | --- |
| Magnet | pulls in sparks from adjacent lanes | 8 s | +1.5 s per level, 5 levels |
| Shield | absorbs one hit | until hit or 20 s | +3 s per level, 5 levels |
| Double Sparks | 2× sparks | 10 s | +2 s per level, 5 levels |

### 3.6 Difficulty
- `speed = min(start_speed + speed_ramp_per_min × minutes, max_speed)`. All three values come from Remote Config (§7.3).
- Chunk tiers unlock by distance: tier 1 at 0–500 m, tier 2 at 500–1,500 m, tier 3 beyond 1,500 m.

### 3.7 New features (what makes it modern)
- **Daily Run:** one fixed-seed track per UTC day, the same for every player. Shows the player's personal best for the day. (A leaderboard comes later.)
- **Missions:** 3 active missions drawn from a pool of at least 20 templates, such as "collect 300 sparks in one run", "slide 15 times" or "run 2 km". Completing one grants sparks and XP, then a new mission rolls in.
- **Courier Level (XP):** XP comes from distance and missions. Each level-up grants a reward and unlocks Locker items.
- **Locker:** 3 characters and 3 trails. These are cosmetic only and never change stats.
- **Upgrades:** power-up durations (5 levels each), bought with sparks.
- **Live events, ready to switch on:** Remote Config flags such as a weekend double-sparks event, enabled without an app update.
- **Fair monetization:** rewarded ads are always optional, interstitials are capped, and Remove Ads is available as a purchase.

### 3.8 Revive
On a crash, if a revive is still available (`revives_per_run`, default 1):
1. Unity shows an in-game prompt, "Continue?", with a 5-second countdown.
2. If the player accepts, Unity sends `ad.request` (placement `revive`).
3. If the ad is granted, Unity clears nearby obstacles, gives 2 seconds of invulnerability, and resumes the run.
4. If the ad is declined, fails or times out, the session ends normally.

### 3.9 Tutorial (first run only; can be replayed from Settings)
- Slower speed and 4 prompts: left, right, jump and slide.
- Each prompt waits for the correct input.
- The player can't lose during the tutorial.
- It ends with "You're ready!" and flows into a normal run.

### 3.10 Feel: audio, haptics, feedback
- An audio mixer with Music and SFX groups, a music loop, and SFX for every action, pickup, power-up, crash and new best.
- Haptics: medium on pickup or power-up, heavy on crash, a success pattern on a new best, and light on UI taps. Players can turn haptics off.
- Visual feedback: particles on pickups, a short camera shake on crash, and "Reduce motion" turns shake off.

### 3.11 Unity performance rules
- Object pooling throughout: no `Instantiate` or `Destroy` during a run.
- Floating origin: reset the world position every 500 m.
- URP with mobile settings, simple lit or unlit stylized shaders, and blob shadows instead of realtime shadows.
- `Application.targetFrameRate = 60`. Aim for about 100 draw calls at most, and use texture atlases.

### 3.12 Art and audio sourcing
- Use your own low-poly models built from primitives or ProBuilder, procedural meshes, and CC0 packs (for example kenney.nl).
- Use CC0 sound effects and CC0 or self-generated music.
- Use one OFL-licensed display font, bundled in the app. Never fetch fonts at runtime.
- Log every external file in `ASSETS_LICENSES.md` with its source URL, license and where it's used.

---

## 4. Modern UI (Flutter shell)

### 4.1 Principles
- **One tap to play** from launch. There is no login wall; the anonymous identity is created in the background.
- **Dark-first neon theme** using the same tokens as the game (§3.1). Material 3 with a custom theme.
- **Thumb-friendly:** primary actions sit at the bottom, and every touch target is at least 48 dp.
- **Motion:** transitions take 150–300 ms, numbers count up, and everything respects "Reduce motion".
- **Never block on the network.** Show cached data instantly and sync quietly in the background.
- **No dead ends:** every screen has a clear next action.
- **Accessibility:** text can scale to 1.3× without clipping, colour is never the only signal, and every control has a TalkBack label.

### 4.2 Screens

| Screen | Purpose | Key elements |
| --- | --- | --- |
| Splash | start-up | logo; services start in parallel; 2 s at most |
| Onboarding (first launch) | teach and get consent | 3 swipe cards (controls, missions, locker), ads consent (§7.4) |
| Home | one-tap play | big Play button, sparks and level, Daily Run card, 3 missions, character preview, "More games coming" card (factory placeholder) |
| Game | the run | full-screen Unity, with no Flutter UI on top during play |
| Results | reward and next step | distance, animated sparks count, new-best celebration, mission progress, XP bar, "Double sparks (ad)", Play again, Home |
| Missions | progress | 3 active missions, claim rewards |
| Locker | cosmetics and upgrades | characters, trails, power-up upgrades, buy with sparks, equip |
| Store | purchases | Remove Ads, Starter Pack, Restore purchases |
| Profile | stats | level, best distance, total runs, total sparks, today's Daily Run best |
| Settings | control | music, SFX, haptics, reduce motion, share usage data, privacy options (consent), privacy policy link, replay tutorial, restore purchases, reset progress (with confirmation), version |

**Navigation:** a bottom bar with Home · Missions · Locker · Store. Profile and Settings open from the top-right corner.

The in-run HUD (distance, sparks, power-up timers and pause) lives in **Unity**, not Flutter.

---

## 5. Architecture

### 5.1 Overview
```
┌───────────────────────── Android app ─────────────────────────┐
│ FLUTTER (owner of everything outside the run)                  │
│  screens · profile & economy · missions · Firebase · ads · IAP │
│            ▲ JSON messages (bridge contract §6) ▼             │
│ UNITY 6 (gameplay only, embedded as a library)                 │
│  run · track · obstacles · power-ups · HUD · audio · haptics   │
└───────────────────────────────────────────────────────────────┘
        │ Auth · Firestore · Analytics · Crashlytics · Remote Config
        ▼
     FIREBASE
```

### 5.2 Key decisions (record any changes in `docs/decisions.md`)

| Decision | Reason |
| --- | --- |
| **Flutter owns all Firebase, ads and IAP; Unity is gameplay only** and forwards analytics events over the bridge | One copy of each SDK avoids native dependency conflicts inside embedded Unity. It also matches the master doc, where Flutter handles business flows. |
| **Unity never changes balances or saves the profile.** It reports run facts; Flutter applies rewards. | This keeps a single owner for the economy and makes rewards testable and idempotent. |
| **Embedding plugin:** `flutter_embed_unity` plus `flutter_embed_unity_6000_0_android` (latest versions). Fallback: `flutter_unity_widget_2` (6000.x). | It explicitly supports Unity 6000.0 and 6000.3 LTS and can export from the command line, which you need for automation. |
| **Unity 6 LTS only:** 6000.3.x, or 6000.0.58f2 or newer | The plugin supports only these versions. They also include an Android security fix and 16 KB page-size support. |
| **URP project, Graphics API = OpenGLES3 only** | The plugin requires URP, and OpenGLES3 is the stable option inside the embed. |
| **Portrait lock plus full `android:configChanges` on MainActivity** | If the host Activity is destroyed, embedded Unity kills the whole app, so a rotation must never recreate it. |
| **Unity stays loaded after the first run** (it can't be unloaded) | This is a plugin limitation. Keep a single Game route, and pause or resume Unity rather than disposing it. |
| **Local-first saves with Firestore sync** | The app must be fully playable offline, including on its very first launch. |
| **Remote Config with in-app defaults and clamped ranges** | Live tuning without updates, while bad values can never break the game. |
| **Android only** | iOS needs macOS and Xcode. |

### 5.3 Android build settings
- `targetSdk 36` (required by Google Play for new apps since 31 August 2026). If the installed Unity can't target 36, report this in PROGRESS.md.
- `minSdk` equal to Unity's Minimum API Level, which is at least 23.
- Match Unity's toolchain. For Unity 6000.2–6000.3 that's Gradle 8.13, AGP 8.10.0, NDK r27c (`27.2.12479018`) and JDK 17. Check Unity's current compatibility table before you start.
- Unity Player Settings: IL2CPP, target architectures ARMv7 + ARM64. For **emulator builds only**, add x86-64 (Chrome OS) (see §8.2). Active Input Handling: "Both".

### 5.4 Reusability (the factory rule)
- Unity: anything that Game #2 could use goes in `Assets/Core/` (bridge, config, audio, input, haptics, pooling, analytics forwarder, autoplay harness base). Game-specific code goes in `Assets/Games/UrbanRunner/`.
- Flutter: shared services go in `lib/core/`, screens in `lib/features/`, and game-specific data (missions pool, items, economy numbers) in `lib/games/urban_runner/`.
- At hand-back, list in `release/TEST_REPORT.md` which parts Game #2 can reuse as they are.

---

## 6. Flutter ↔ Unity bridge contract
Keep a copy of this section in `docs/bridge-contract.md` and keep it in sync with the code.

**Transport.** Flutter calls `sendToUnity("FlutterBridge", "OnFlutterMessage", json)`. Unity replies with `SendToFlutter.Send(json)`. On the Unity side, wrap the transport behind an `IBridgeTransport` interface, so standalone Unity builds can use a `StandaloneHostTransport` that fakes Flutter.

**Envelope:**
```json
{ "v": 1, "type": "session.start", "sessionId": "uuid-v4", "payload": { } }
```

**Flutter → Unity**

| type | payload |
| --- | --- |
| `session.start` | `seed`, `mode` (`normal` / `daily` / `tutorial`), `character`, `trail`, `upgrades{magnet,shield,double}`, `config{start_speed,max_speed,speed_ramp_per_min,revives_per_run,revive_enabled}`, `settings{music,sfx,haptics,reduceMotion}`, optional `testMode{autoplay,skill(0–1),endAfterSeconds,forceCrashAtSeconds}` |
| `ad.result` | `placement`, `granted` (bool) |
| `game.pause` / `game.resume` | – |
| `settings.update` | `music`, `sfx`, `haptics`, `reduceMotion` |

**Unity → Flutter**

| type | payload |
| --- | --- |
| `unity.ready` | `bridgeVersion`, `buildVersion` (sent after the scene loads) |
| `session.started` | – |
| `ad.request` | `placement` (`revive`) |
| `analytics.event` | `name`, `params` |
| `session.ended` | `mode`, `seed`, `distanceM`, `sparks`, `durationS`, `cause` (`obstacle` / `quit` / `test`), `revivesUsed`, `stats{jumps,slides,laneChanges,magnet,shield,doubleSparks}`, `buildVersion` |
| `error` | `code`, `message` |

**Rules**
1. Flutter waits for `unity.ready` before sending anything. After 10 s with no message, it shows an error state with a Retry button.
2. Flutter applies the rewards from a `session.ended` **exactly once per `sessionId`**. Store the IDs it has already processed.
3. Flutter sanity-checks results: if sparks exceed what the distance makes possible, it caps them and logs `result_rejected`.
4. After `ad.request`, Unity waits at most 60 s for `ad.result`. If none arrives, it treats the ad as not granted.
5. Unknown message types are logged and ignored. A version mismatch produces an `error` message.
6. All numbers are written using the invariant culture (a dot as the decimal separator).
7. Missions are evaluated in **Flutter** from the run's `stats`.
8. `sparks` in `session.ended` already include the Double Sparks power-up, because that's gameplay. The Remote Config multipliers (`sparks_multiplier`, and `event_sparks_multiplier` when `event_active` is true) are applied **only by Flutter**, at reward time, so nothing is counted twice.

---

## 7. Data, analytics, configuration and monetization

### 7.1 Saves and Firestore
**Local-first.** The profile lives on the device and syncs to Firestore whenever the device is online. Merge rules: take the maximum for fields that only ever increase (best distance, total runs, XP), the latest write for selections, and the union for owned items.

```
users/{uid}
  createdAt, lastActiveAt, schemaVersion
  sparks (int ≥ 0), xp (int ≥ 0), level (int ≥ 1)
  selectedCharacter, selectedTrail
  ownedItems [string], upgrades {magnet,shield,double: 0–5}
  settings {…}
games/urban_runner/players/{uid}
  bestDistance (only increases), totalRuns (only increases)
  dailyBest {date: "YYYY-MM-DD", distance}
  missions [ … ], updatedAt
purchases/{purchaseId}   ← server only (future); clients can't read or write
```

**Security rules (must have tests):**
- All access requires sign-in.
- Users can read and write only their own documents.
- Writes are validated for field types and ranges, and `createdAt` can't change.
- `bestDistance` and `totalRuns` can never decrease.
- `sparks` can rise by at most 20,000 in a single write (a sanity cap).
- `purchases` is denied to clients. Everything else is denied by default.

Firestore rules can't fully protect the economy; that needs a server, which is a later phase. Note this in `KNOWN_ISSUES.md`.

**Until the owner provides a real Firebase project,** use the Firebase Emulator Suite (Auth + Firestore) with project ID `demo-urban-runner`. Analytics, Crashlytics and Remote Config sit behind interfaces with debug implementations (a logging sink and local defaults). The real implementations switch on automatically when `google-services.json` is present.

### 7.2 Analytics events
Use snake_case names of 40 characters or fewer and avoid Firebase's reserved event names. `first_open`, `session_start` and `screen_view` are logged automatically.

| Event | Params |
| --- | --- |
| `tutorial_begin` / `tutorial_complete` | `duration_s` |
| `game_start` | `session_id`, `mode`, `character` |
| `game_end` | `session_id`, `mode`, `distance_m`, `sparks`, `duration_s`, `cause`, `revives` |
| `revive_offer` / `revive_accept` / `revive_decline` | `session_id` |
| `ad_rewarded_start` / `ad_rewarded_complete` / `ad_rewarded_fail` | `placement` |
| `ad_interstitial_show` | `runs_since_last` |
| `iap_start` / `iap_complete` / `iap_cancel` / `iap_fail` | `product_id` |
| `mission_complete` | `mission_id`, `reward` |
| `level_up` | `level` |
| `item_unlock` | `item_id`, `source` (`sparks` / `level` / `iap`) |
| `upgrade_buy` | `powerup`, `level` |
| `daily_run_complete` | `distance_m`, `is_best` |
| `config_invalid` | `key` |
| `result_rejected` | `reason` |

### 7.3 Remote Config (defaults ship in the app; out-of-range values are clamped and logged)

| Key | Default | Allowed range |
| --- | --- | --- |
| `start_speed` | 8 | 5–12 |
| `max_speed` | 22 | 12–30 |
| `speed_ramp_per_min` | 3 | 0.5–6 |
| `revive_enabled` | true | – |
| `revives_per_run` | 1 | 0–2 |
| `double_reward_enabled` | true | – |
| `sparks_multiplier` | 1.0 | 0.5–3.0 |
| `interstitial_first_after_runs` | 3 | 2–10 |
| `interstitial_every_n_runs` | 3 | 2–10 |
| `interstitial_min_interval_s` | 120 | 60–600 |
| `event_active` | false | – |
| `event_sparks_multiplier` | 2.0 | 1.0–3.0 |

Fetch in the background, and **activate on the next app start**, never in the middle of a run.

### 7.4 Ads (Google Mobile Ads, in Flutter)
- **Consent:** run Google's UMP consent flow before requesting any ads, and add a "Privacy options" entry in Settings where consent rules require one.
- **Test IDs only:** use Google's official AdMob test app ID and test ad unit IDs (see the AdMob docs). Real IDs arrive later through build configuration and must never appear in debug builds.
- **Rewarded placements:** `revive` (in-run) and `double_reward` (Results screen).
- **Interstitial policy** (every number comes from §7.3):
  - Never during the first session, the tutorial, or before run `interstitial_first_after_runs`.
  - At most once every `interstitial_every_n_runs` runs **and** every `interstitial_min_interval_s` seconds.
  - Only when the player leaves Results (Play again or Home), never mid-run, and never right after a rewarded ad.
  - Never shown to players who own Remove Ads.
- Pause Unity before any full-screen ad and resume it afterwards.

### 7.5 In-app purchases (`in_app_purchase` plugin)

| product_id | Type | Grants |
| --- | --- | --- |
| `remove_ads` | non-consumable | no interstitials (rewarded ads stay available as an option) |
| `starter_pack` | one-time | 2,000 sparks + the exclusive "Lantern" trail |

- Handle buy, cancel, pending, error and restore.
- Grant each purchase **exactly once** per purchase token, and always complete (acknowledge) the purchase so Play doesn't refund it.
- In v1, read entitlements from Play Billing on the device (check past purchases at start-up) and cache them locally.
- Use a `FakeStore` implementation for all automated tests. A real purchase test needs Play Console internal testing, which is an owner task.

---

## 8. Environment and toolchain (owner's machine)

### 8.1 Check these first (M0)

| Tool | Needed for | Check |
| --- | --- | --- |
| Unity 6 LTS: **6000.3.x** (or 6000.0.58f2+) with **Android Build Support** (OpenJDK, Android SDK & NDK) | game | Unity Hub editor list; `<Unity> -version` |
| Android SDK, emulator, adb | testing | `adb devices`, `emulator -list-avds` |
| AVD: x86_64, API 35 or 36, Google APIs or Google Play image, ≥ 4 GB RAM, Graphics = Hardware | testing | AVD config |
| Flutter (latest stable) | app shell | `flutter doctor -v` |
| JDK 17 | Gradle | `java -version` |
| Node.js LTS + Firebase CLI | Emulator Suite, rules tests | `node -v`, `firebase --version` |
| Git | version control | `git --version` |

You may install Flutter, Node.js, the Firebase CLI and Android SDK packages (cmdline-tools, build-tools, platform 36, NDK r27c, a low-spec test AVD). **Tell the owner what you'll install and why, and get approval first.** Unity editors and modules can only be installed by the owner through Unity Hub.

If the installed Unity is 6000.1 or 6000.2 (non-LTS), or an older 6000.0 patch, stop and ask the owner to install 6000.3 LTS.

### 8.2 The emulator caveat
Unity's Android player is built for ARM processors, while most PCs and their emulators are x86. That has two consequences:
1. For **emulator builds**, also enable the **x86-64 (Chrome OS)** target architecture. It isn't officially supported for phones and may show visual glitches. Never include it in store builds.
2. Unity running *inside Flutter* may still fail on an x86 emulator; the plugin authors list emulators as unsupported. On an Apple Silicon Mac, ARM emulator images run the normal build without trouble.

This is why testing is layered (§10). Unity runs on its own on the emulator, Flutter runs with a fake Unity on the emulator, and the combined app runs on the emulator **if the M0 spike passes**. If it doesn't, the combined app is tested on a physical Android phone over USB (owner task 6).

### 8.3 Unity batchmode notes
- **Close the Unity Editor** on this project before running batchmode. The project is locked while the Editor has it open.
- On Windows, `Unity.exe` returns immediately. Use `Start-Process -Wait` (PowerShell) or `start /wait` (cmd).
- `-runTests` exits on its own, so **don't** add `-quit` to test runs.
- Always pass `-logFile <path>`, and read the log when something fails.
- A license error means the owner needs to sign in to Unity Hub.
- Store the detected Unity path in `tools/env.local`, which is gitignored.

```bash
# EditMode / PlayMode tests
"$UNITY" -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode \
  -testResults build/test-results/editmode.xml -logFile build/logs/editmode.log
"$UNITY" -batchmode -projectPath unity -runTests -testPlatform PlayMode \
  -testResults build/test-results/playmode.xml -logFile build/logs/playmode.log

# Export Unity into the Flutter app (flutter_embed_unity)
"$UNITY" -batchmode -nographics -projectPath unity -buildTarget Android \
  -executeMethod ProjectExporterBatchmode.ExportProject \
  -exportPath app/android/unityLibrary -logFile build/logs/export.log -quit

# Standalone Unity APK for emulator gameplay tests (your own build script)
"$UNITY" -batchmode -nographics -projectPath unity -buildTarget Android \
  -executeMethod BuildScripts.BuildStandaloneEmulatorApk -logFile build/logs/standalone.log -quit

# Flutter
cd app && flutter analyze && flutter test
flutter test integration_test -d <emulator-id>
flutter build appbundle --release

# Firestore rules tests
firebase emulators:exec --project demo-urban-runner --only firestore,auth "npm --prefix firebase test"
```

---

## 9. Milestones

At the end of every milestone:
- Commit the work.
- Update `PROGRESS.md`.
- Save an APK snapshot to `release/milestones/`.
- Save screenshots to `release/screenshots/mN/`.
- Write the owner a five-line summary: what's done, test results, where the screenshots are, and anything you need from them.

### M0 — Environment, repo and bridge spike
**Do:**
- Check the tools (§8.1) and report one list: what's present, what's missing, and what you need from the owner.
- Create the repo layout (§14).
- Create the Unity URP project. If the command line can't create it, ask the owner to create it in Unity Hub using the Universal 3D template, inside `unity/`.
- Create the Flutter app in `app/` and import the plugin's Unity package.
- Build a test scene (a rotating cube, a `FlutterBridge` object, and a ping handler), then export it and embed it in a Flutter screen.
- Write the `tools/` scripts (§14) for both PowerShell and bash.

**Spike checks on the emulator** (with x86-64 enabled):
1. Unity renders.
2. Touch and swipe input work.
3. A ping→pong round trip takes under 100 ms.
4. Pause and resume work.
5. Leaving the screen and returning works.
6. The app survives background/foreground.
7. Rotating the emulator causes no crash.

**Exit:**
- `tools/test_all` runs green.
- The spike results are in `docs/decisions.md`, with a verdict on whether the combined app is testable on the emulator.
- If the spike still fails after 3 serious approaches (the other plugin, graphics settings, architecture settings), record what you tried, continue, and flag owner task 6.

### M1 — Core gameplay (Unity, standalone)
**Do:**
- Player controller with swipe and keyboard input.
- Seeded chunk generator.
- The 4 obstacle types, sparks, and the 3 power-ups.
- Speed ramp, crash, the revive hook, HUD and pause.
- Pooling and floating origin.
- The **autoplay bot** (looks ahead and dodges, with a skill setting).
- The standalone host.

**Exit:**
- EditMode tests pass: generator guarantees, difficulty curve, power-up timers and run stats.
- PlayMode tests pass:
  - Autoplay runs 3 minutes with no exceptions.
  - A forced crash produces the correct `session.ended`.
  - A revive resumes the run.
- The standalone emulator APK autoplays for 5 minutes cleanly, with FPS logged.

### M2 — Feel, polish and original look
**Do:**
- Art pass for Lantern City (§3.1, §3.12): animation, camera, particles.
- Audio mixer, music and SFX, and haptics.
- Tutorial.
- Difficulty tuning and a performance pass.

**Exit:**
- The tutorial PlayMode test passes.
- The run loop makes no per-frame allocations.
- Performance numbers are recorded.
- `ASSETS_LICENSES.md` is complete, and screenshots are saved.

### M3 — Modern Flutter shell and integration
**Do:**
- Theme tokens and every screen in §4.2.
- Local profile store.
- Missions, Daily Run, XP, Locker and upgrades logic.
- Bridge client with a `FakeUnityGame` for tests.
- Results screen and idempotent rewards; settings forwarded to Unity.
- The combined app.

**Exit:**
- Unit and widget tests pass.
- Integration tests on the emulator (with the fake Unity) cover onboarding → play → results → persistence after restart.
- **Combined smoke test:** 3 complete runs of Home → Game → Results → Home, on the emulator or a phone (per the M0 verdict).

### M4 — Firebase
**Do:**
- Anonymous auth and local-first Firestore sync with the merge rules.
- Security rules with tests.
- Analytics service with the §7.2 schema and a debug sink.
- Crashlytics wiring, and Remote Config with clamping.

**Exit:**
- Rules tests pass. They must show that another user's document is denied, invalid writes are rejected, and client writes to `purchases` are denied.
- Auth and sync tests pass against the Emulator Suite.
- Offline first launch → play → reconnect leaves the profile synced.
- The analytics assertions and the invalid-Remote-Config-value tests pass.

### M5 — Monetization
**Do:**
- UMP consent.
- Rewarded ads (revive, double reward) and the interstitial policy.
- Remove Ads and Starter Pack, with restore.

**Exit:**
- Tests pass for:
  - The interstitial policy, simulated over time and run counts.
  - Rewards under duplicate callbacks.
  - The purchase, cancel, pending and restore state machine with `FakeStore`.
- On the emulator, test ads show and grant their reward exactly once.
- No ads appear in the tutorial or the first session.

### M6 — Full QA and hand-back
**Do:**
- Run the whole E2E checklist (§10.3) and fix every failure.
- Build the release outputs and run the 16 KB alignment check.
- Write the hand-back files (§11).

**Exit:** every gate in §11 is green, or the failure is listed in `KNOWN_ISSUES.md` with the reason and what the owner must decide.

---

## 10. Testing

### 10.1 Test layers

| Layer | Tool | Where | When |
| --- | --- | --- | --- |
| Unity logic | Unity Test Framework, EditMode | batchmode | every change |
| Unity gameplay | PlayMode + autoplay bot | batchmode | every change |
| Unity on Android | standalone emulator APK + autoplay | emulator | M1, M2, M6 |
| Flutter logic / UI | `flutter test` (unit, widget) | host | every change |
| Flutter flows | `integration_test` + `FakeUnityGame` | emulator | M3+ |
| Backend rules | `@firebase/rules-unit-testing` on the Emulator Suite | host | M4+ |
| Combined app | `integration_test` with real Unity in `testMode.autoplay`, plus adb scripts | emulator or phone | M3, M6 |
| Robustness | monkey, background/kill, offline, rotation | emulator | M6 |

**Required tests:**
- The generator never produces an impossible row. Check 100 seeds × 10,000 rows, including whether a passable lane can actually be reached in time.
- Reward idempotency.
- The interstitial policy.
- The Remote Config clamping.
- The bridge contract, by round-tripping every message type on both sides.

Every bug fix also gets a regression test.

### 10.2 adb toolbox
```bash
adb devices                                          # find the emulator id
adb install -r <apk>                                 # install / update
adb shell am start -W -n <pkg>/<activity>            # launch; prints start-up time
adb logcat -c && adb logcat -v time > build/logs/logcat.txt
adb shell screencap -p /sdcard/s.png && adb pull /sdcard/s.png release/screenshots/<name>.png
adb shell dumpsys meminfo <pkg>                      # memory
adb shell input keyevent KEYCODE_HOME                # send app to background
adb shell am kill <pkg>                              # process death while backgrounded
adb shell svc wifi disable && adb shell svc data disable   # go offline (enable to restore)
adb emu rotate                                       # rotate the emulator
adb shell monkey -p <pkg> --throttle 150 --pct-syskeys 0 -v 2000
zipalign -c -P 16 -v 4 <apk>                         # 16 KB alignment check
```
Scan the logcat output for `FATAL EXCEPTION`, `ANR in`, native crash markers and `UR_ERROR`. Tag your own logs `UR_GAME`, `UR_BRIDGE`, `UR_PERF` and `UR_ERROR`.

### 10.3 End-to-end checklist (from master doc §11)

| # | Check | How | Where |
| --- | --- | --- | --- |
| 1 | Fresh install → onboarding → first run | uninstall, install, integration test | emulator |
| 2 | Unity opens from Flutter and returns cleanly | combined smoke, 3 runs | emulator or phone |
| 3 | Session start/end and result reporting | contract tests + combined smoke | host + device |
| 4 | Anonymous identity stays the same | Auth emulator; kill and relaunch, compare uid | emulator |
| 5 | Progress persists across restart and process death | integration test + `am kill` | emulator |
| 6 | Offline play and recovery | `svc` off → play → on → check sync | emulator |
| 7 | Analytics events with correct params | debug-sink assertions (DebugView once real Firebase exists) | host / emulator |
| 8 | Remote Config changes apply safely | valid, edge and invalid values | host |
| 9 | Rewarded ad request → complete → reward once | test ads + idempotency tests | emulator + host |
| 10 | Interstitial frequency and cooldown | policy tests | host |
| 11 | IAP buy, cancel, pending, restore | `FakeStore` tests; real flow is owner task 8 | host |
| 12 | Crashlytics test crash | hidden debug-only dev menu; only with real Firebase | emulator |
| 13 | Low-end performance and memory | low-spec AVD (2 GB RAM, 720p); real phone if available | emulator / phone |
| 14 | iOS | out of scope | – |
| 15 | Background / foreground mid-run | HOME key → relaunch → paused, resumes cleanly | emulator |
| 16 | Orientation | rotate in menus and mid-run → stays portrait, no restart | emulator |
| 17 | Audio and haptics settings | `settings.update` test + manual check | emulator |
| 18 | No duplicate rewards | send `session.ended` and ad callbacks twice | host |
| 19 | Security rules block unauthorized access | rules tests | host |
| 20 | Store build validation | release AAB, targetSdk 36, 16 KB check | host |

### 10.4 Budgets

| Budget | Target |
| --- | --- |
| Frame rate | 60 fps target; report average and minimum per run (emulator numbers are only indicative) |
| Run-loop allocations | 0 B GC per frame after warm-up |
| Cold start → Home | ≤ 3 s (release build, emulator) |
| Tap Play → running | ≤ 5 s the first time (Unity cold load), ≤ 1.5 s after that |
| Memory during a run | ≤ 500 MB PSS |
| Download size (arm64) | ≤ 80 MB |
| Crashes / ANRs | 0 across every automated run and the 2,000-event monkey test |

---

## 11. Definition of done and hand-back

### 11.1 Release gates (from master doc §12)

| Gate | Must be true | Evidence |
| --- | --- | --- |
| Gameplay | core loop stable and fun; tutorial works; no blocker bugs | test results, autoplay logs, screenshots |
| Performance | §10.4 budgets met, or deviations explained | `UR_PERF` logs, meminfo |
| Monetization | ads and IAP work; no duplicate rewards; progression never blocked | tests + emulator test ads |
| Analytics | every §7.2 event fires with correct params | test output |
| Backend | auth, rules and persistence verified | rules + sync tests |
| Stability | no crashes or ANRs | logcat scans, monkey log |
| Store build | AAB builds, targetSdk 36, 16 KB aligned, versioned 0.1.0 | build log, zipalign output |
| IP | only original or licensed assets | `ASSETS_LICENSES.md` |

### 11.2 Hand back this folder
```
release/
├─ UrbanRunner-0.1.0-emulator.apk   includes x86-64, for the owner's emulator
├─ UrbanRunner-0.1.0-phone.apk      ARM only, for real phones
├─ UrbanRunner-0.1.0.aab            store format (needs owner's upload key before Play upload)
├─ HOW_TO_RUN.md                    install and play in 3 steps, written for a non-developer
├─ TEST_REPORT.md                   every check in §10.3 and §11.1: how, result, evidence link; plus reusable parts for Game #2
├─ KNOWN_ISSUES.md
├─ OWNER_TASKS.md                   what's left for the owner, in order
├─ screenshots/                     every screen + gameplay
├─ milestones/                      APK snapshots
└─ logs/                            test result XML, logcat from smoke and monkey runs
```
Finish with a short message to the owner: what works, what to install first, and the top 3 owner tasks.

---

## 12. Owner tasks (the only reasons to stop and ask)

| # | Task | When |
| --- | --- | --- |
| 1 | Confirm Unity is 6000.3 LTS (or 6000.0.58f2+) with Android Build Support; install it through Unity Hub if missing | M0 |
| 2 | Approve tool installs and accept the Android SDK licenses | M0 |
| 3 | Keep the Unity Editor closed while Claude Code builds or tests, or say when it's open | always |
| 4 | Choose the final package ID (e.g. `com.yourstudio.urbanrunner`). It can't change after publishing; use `com.gamefactory.urbanrunner` until then | before M4 |
| 5 | Create a Firebase project, add the Android app, and place `google-services.json` in `app/android/app/` (or sign in when `firebase login` asks). Optional for building, since the Emulator Suite covers development | M4 / later |
| 6 | Connect an Android phone with USB debugging if the M0 spike says the emulator can't run the combined app | M3 / M6 |
| 7 | AdMob account and real ad unit IDs | before store |
| 8 | Play Console: the app, the `remove_ads` and `starter_pack` products, an internal testing track and license testers | before store |
| 9 | Privacy policy URL, Data safety form, content rating and target audience | before store |
| 10 | Final name and trademark check; store listing graphics | before store |
| 11 | Upload key and Play App Signing | before store |
| 12 | Firebase Blaze plan, for later server-side purchase verification | later phase |

---

## 13. Working rules for Claude Code
1. Work in small steps: plan → implement → test → commit. Use clear commit messages.
2. Never mark a milestone done with failing or skipped tests. Treat a flaky test as a bug.
3. Keep `PROGRESS.md` up to date (status per milestone, what's next, blockers) and `docs/decisions.md` (date, decision, reason, alternatives).
4. If 3 different approaches to the same problem fail, write it up under "Blocked" in `PROGRESS.md` and move on to other work.
5. Before adding a package, check its current version and maintenance status on pub.dev or in the Unity docs. Then pin the version.
6. Never edit the generated `app/android/unityLibrary` by hand; re-export instead.
7. Never commit secrets, keystores or `google-services.json`. Never use real ad IDs in debug builds.
8. Never create accounts, spend money, accept terms on the owner's behalf, or publish anything.
9. Shipped code may contain no TODOs, except ones listed in `KNOWN_ISSUES.md`.

---

## 14. Repo layout
```
game-factory/
├─ CLAUDE.md  HANDOVER.md  PROGRESS.md  ASSETS_LICENSES.md
├─ docs/            decisions.md, bridge-contract.md
├─ unity/           Unity 6 URP project
│  └─ Assets/
│     ├─ Core/                Bridge, Config, Audio, Input, Haptics, Pooling, Analytics forwarder, Autoplay base
│     ├─ Games/UrbanRunner/   Player, Track, Obstacles, Pickups, PowerUps, Difficulty, HUD, Tutorial, Scenes
│     ├─ SharedAssets/        font, shaders, palette
│     └─ Tests/               EditMode/, PlayMode/
├─ app/             Flutter shell
│  ├─ lib/core/               bridge, analytics, ads, iap, remote_config, auth, storage, theme
│  ├─ lib/features/           onboarding, home, game, results, missions, locker, store, profile, settings
│  ├─ lib/games/urban_runner/ missions pool, items, economy numbers
│  ├─ test/  integration_test/
│  └─ android/unityLibrary/   generated by export (gitignored)
├─ firebase/        firebase.json, firestore.rules, tests/
├─ tools/           env detection, test_all, build_all, emulator_smoke (.ps1 + .sh)
├─ build/           logs, test results (gitignored)
└─ release/         hand-back package (§11)
```
`tools/test_all` runs every automated test layer that's available and exits non-zero on any failure. `tools/emulator_smoke` installs the emulator APK, runs autoplay sessions, captures logcat and screenshots, and fails if it finds any crash marker.
