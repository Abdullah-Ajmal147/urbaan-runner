# Progress: Urban Runner

_Last updated: 2026-10-10. Tests: Unity EditMode 126/126, PlayMode 8/8, Flutter 98/98, Firestore rules 10/10, tools 5/5._

| Milestone | Status | Exit test |
| --- | --- | --- |
| M0 Environment, repo, bridge spike | ✅ done 2026-10-05 | test_all green; spike 7/7 on the emulator (D10) |
| M1 Core gameplay (Unity standalone) | ✅ done 2026-10-05 | EditMode 79/79, PlayMode 5/5, 5 min autoplay clean |
| M2 Feel, polish, original look | ✅ done 2026-10-06 | see below; performance re-measured on a real phone is the open item |
| M3 Flutter shell + integration | ✅ done 2026-10-06 | widget/flow tests; on-device flows; real phone APK tested by the owner |
| M4 Firebase | ✅ done 2026-10-10 | project `urban-runner-5c944` (D34); anonymous auth + Firestore sync verified on the emulator against the real project; rules 10/10 |
| M5 Monetization | ✅ done 2026-10-10 (test ads) | real Google test ads on the emulator: revive granted once, interstitial paced, 0 ads in the first session; FakeStore state machine; real ad/product IDs wait on owner tasks 5–6 (D35) |
| M6 Full QA + hand-back | 🟡 started | negative-test pass done (`docs/qa-negative-tests.md`); release folder still to do |

Test builds for the owner's phone: `release/test-builds/UrbanRunner-0.1.0-phone-arm64.apk` (most phones) and `…-phone-arm32.apk` (older phones). These are not committed; rebuild them with the commands in "How to rebuild" below.

---

## Done so far

### Gameplay (Unity)
- **Look, "Lantern Festival at dusk" (D27):**
  - Gradient sunset sky, every building painted, terracotta pavements and tram rails.
  - Lantern strings, light pools on the road, and floating sky lanterns with fireworks.
  - Toon shading.
- **Courier at real scale (1.75 m)** with a high, pulled-back chase camera that rises onto roofs.
- **The Warden** (an original patrol drone) chases the courier. A side bump makes you stumble and brings it close; a second bump within 8 s means you're caught (D28).
- **Trams with walkable roofs**, boarding ramps, roof spark lines and crossing between roofs.
- **Gravity jump** (0.8 s air time), lane changes in mid-air, a fast drop into a slide.
- **Speed you can feel:** "Faster!" moments, wind streaks, music speeding up, the FOV widening.
- **Juice (D32):**
  - gem combo ×N with rising notes, and a bounce on the gem counter
  - landing dust and squash
  - "Close!" near-miss slow motion
- **Trails visible in the run** (Neon Ribbon, Paper Kite, Lantern). They were sold but never shown before.
- **Lantern Hunt lantern** with a light beam (one per run).

### Performance (D19, D29, D31)
- **Draw calls:** about 400–490 → 67–93.
  - Street and obstacles are merged into one mesh each, with colours baked into the vertices.
  - Sparks and sky lanterns use GPU instancing.
- **Unity is shown with Hybrid Composition** (no more off-screen copy of every frame). A native "kick" in `MainActivity` gives Unity window focus.
- **Adaptive quality:** render scale 0.85 → 0.55, then post-processing off, then a steady 30 fps. The level is remembered per phone, with a pixel budget for 1440p screens.
- Bloom removed (it did nothing with HDR off), shader warm-up during loading, Swappy frame pacing, ASTC textures, zero allocations per frame (a test proves it).

### App (Flutter)
- Every screen: splash, onboarding, Home, Missions, Locker, Store, Profile, Settings, game, Results.
- Local-first economy, with rewards applied once per sessionId and malformed results sanitised (N19).
- **Daily streak gift (D32):** a 7-day calendar, 100 → 1,500; missing a day resets it; once per day; a flame on Home.
- **Lantern Hunt (D32, bridge v3):** 5 lanterns a day, a growing prize for consecutive days, a Home card and a Results row.
- **Branded loading cover** instead of Unity's splash plus a spinner (D33).
- **Back button:** pauses a run (D30); on a tab it goes to Home first; on Home it exits.

### Quality
- **Negative test pass** (`docs/qa-negative-tests.md`): 13 issues found, 11 fixed, each with a regression test; 2 could not be reproduced and are logged.
- Tools:
  - `tools/test_all`, `tools/build_all`
  - `tools/emulator_smoke` (including `combined`)
  - `tools/ingame_shots.sh` (design frames without an emulator)
  - `tools/screen_tour.sh`
  - `tools/device_perf.sh` (real-phone FPS)
- Bridge contract v3 (`docs/bridge-contract.md`); decisions D1–D36 (`docs/decisions.md`).

---

## Remaining

### Waiting on the owner (HANDOVER §12)
| # | Item | Needed for |
| --- | --- | --- |
| 1 | Test the latest phone APK; report lag or anything that feels wrong (screenshots help) | tuning |
| 2 | Connect the phone by USB with USB debugging on, so `tools/device_perf.sh` can measure real FPS | performance tuning |
| 3 | ✅ Done 2026-10-13: keep `com.gamefactory.urbanrunner` (D36) | before Firebase/store |
| 4 | ✅ Done 2026-10-10: Firebase project `urban-runner-5c944` (D34) | real cloud saves, analytics, Crashlytics |
| 5 | AdMob account + real ad unit IDs | real ads (store) |
| 6 | Play Console: the app, `remove_ads` and `starter_pack` products, internal testing track, license testers | real purchases (store) |
| 7 | Privacy policy URL, Data safety form, content rating, target audience | store |
| 8 | Final name + trademark check, store listing graphics | store |
| 9 | ✅ Upload key created 2026-10-13 at `~/cred/urban-runner/` (owner must back it up); Play App Signing on first upload (D36) | store |
| 10 | ✅ Done: github.com/Abdullah-Ajmal147/urbaan-runner | backup |

### Claude's next work, in order
1. **Performance on the real phone:** measure with `tools/device_perf.sh`, tune the quality ladder, reduce overdraw from big glow sprites on weaker phones, and add thermal awareness (Unity Adaptive Performance, Android provider). Try Vulkan as an experiment.
2. **Lantern visual check:** capture a frame with the hunt lantern in view (`InGameShots`) and tune its size and beam.
3. **M4 follow-ups:**
   - Confirm in the console that Crashlytics shows up after the first real-phone session.
   - Add the release SHA-1 once the upload key exists (owner task 9).
4. **M5 follow-ups (need the owner's accounts):**
   - Real AdMob IDs in the release build (`-PurAdmobAppId=…`, `--dart-define=UR_ADMOB_REWARDED/INTERSTITIAL=…`).
   - Create the Play Console products, then do a purchase test on an internal testing track.
5. **Retention, next ideas:**
   - Local notifications ("your daily gift is ready", "lantern hunt resets soon")
   - Weekly missions and a season pass-lite
   - Results "so close to your best" moments
   - More couriers and trails
6. **M6 QA + hand-back:**
   - Rerun the full HANDOVER §10.3 checklist on the final build, plus a low-spec AVD (2 GB, 720p).
   - ✅ Release AAB (upload-signed, ARM-only, 16 KB checked by `store_check.py`); still to do: phone APK rebuild via `build_all`.
   - The `release/` folder: HOW_TO_RUN, TEST_REPORT (with evidence for §10.3 and §11.1), KNOWN_ISSUES, OWNER_TASKS, screenshots, logs, milestones.

### Known issues (details in KNOWN_ISSUES.md)
- K1: Unity batchmode sometimes hangs on shutdown. The watchdog handles it, including a success marker for the Flutter export.
- K2: Firestore rules can't fully protect the economy (needs a server, later phase).
- K3: anonymous accounts are tied to the install.
- K4: cloud merge uses "max", not "sum", for runs across devices.
- I7: one ANR in 15,000 monkey events inside Unity's volume listener; not reproducible.
- I5: Unity's "not enough storage" dialog, seen once after the emulator crashed mid-install.

---

## How to rebuild the test APKs
```bash
tools/test_all.sh                       # all tests
source tools/env.sh && UR_SUCCESS_MARKER="Building project for Flutter succeeded" \
  ur_unity export_emulator -nographics -quit -buildTarget Android \
  -executeMethod BuildScripts.ExportForFlutter -exportPath "$ROOT/app/android/unityLibrary" -emulator
(cd app && flutter build apk --release --split-per-abi --target-platform android-arm,android-arm64,android-x64)
# outputs: app/build/app/outputs/flutter-apk/app-{arm64-v8a,armeabi-v7a,x86_64}-release.apk
```

## History
- **M0 (2026-10-05):** toolchain, repo, Unity URP project, Flutter shell, and the bridge spike (7/7 checks on the x86-64 emulator).
- **M1 (2026-10-05):** a deterministic C# simulation, the generator with fairness guarantees, the autoplay bot, the session protocol and the standalone host.
- **M5 (2026-10-10):**
  - Google Mobile Ads (UMP consent, Google test IDs) and Play Billing are in the app (D35).
  - Found and fixed with regression tests:
    - A release crash at launch (R8 removed WorkManager's Room constructor).
    - An ad-revive offer in the tutorial and first session.
    - An interstitial counted as shown when none had loaded.
  - The screen tour now fails immediately on a crash.
- **M4 (2026-10-10):**
  - Created the Firebase project and connected the app to it (D34): anonymous auth, Firestore cloud saves (including the streak and hunt), Analytics, Crashlytics and Remote Config.
  - Without `google-services.json` the app still builds and runs local-only.
  - On the emulator against the real project: the screen tour is clean, and `sync ok` appears after a run.
- **M2/M3 (2026-10-05/06):** art, audio, haptics, the tutorial and the full Flutter shell. Then the in-game redesign (D27/D28), performance (D29/D31), negative testing, retention features (D32) and the loading cover (D33).
- **Store prep (2026-10-13):** upload key created outside the repo; signed ARM-only AAB `release/UrbanRunner-0.1.0.aab` (108 MB); fixed x86_64 leaking into store builds, with a gate and regression test (D36).
