# Progress — Urban Runner

| Milestone | Status | Exit test |
| --- | --- | --- |
| M0 Environment, repo, bridge spike | ✅ done 2026-10-05 | test_all green; spike 7/7 on emulator (docs/decisions.md D10) |
| M1 Core gameplay (Unity standalone) | ✅ done 2026-10-05 | EditMode 79/79, PlayMode 5/5, standalone APK 5 min autoplay clean |
| M2 Feel, polish, original look | ⏳ next | – |
| M3 Flutter shell + integration | – | – |
| M4 Firebase | – | – |
| M5 Monetization | – | – |
| M6 Full QA + hand-back | – | – |

## M0 — done
- **Toolchain:** Unity 6000.3.25f1 LTS + Android (JDK 17, NDK r27c, SDK 36), Flutter 3.47.6 / Dart 3.13.5, Node 22.23, firebase-tools 15.32.1, Android SDK platform 36 / build-tools 36.0.0 / NDK r27c, AVDs `ur_api36` and `ur_lowspec`. All installed in the owner's home folder with approval.
- **Repo:** layout per HANDOVER §14, git initialised.
- **Unity:** URP project, configured by code (`AndroidProjectConfigurator`). Core contains the bridge (`BridgeEnvelope`, `BridgeRouter`, `FlutterBridge`, `IBridgeTransport` + `FlutterEmbedTransport` / `StandaloneHostTransport`), swipe input with the 0.15 s buffer, tagged logging, and build scripts (`BuildScripts.Configure`, `.ExportForFlutter`, `.BuildStandaloneEmulatorApk`). There's also a spike scene.
- **Flutter:** `app/` with flutter_embed_unity 2.0.0, `UnityBridge` (ready gating, 10 s timeout + retry, version check) and a spike screen.
- **Tools:** `env`, `test_all`, `build_all`, `emulator_smoke` (.sh + .ps1), `spike_m0.sh`, `lib/adb.sh`.
- **Tests:** `tools/test_all` ALL GREEN. Unity EditMode passes (24 M0 tests, now 60 including the M1 simulation tests already written) and PlayMode 3/3; Flutter analyze is clean and 16/16 tests pass.
- **Spike:** all 7 checks pass on the x86_64 emulator, so the combined app is testable on the emulator.
- **Evidence:** `release/screenshots/m0/`, `release/logs/m0_spike_*`, `release/milestones/UrbanRunner-m0-spike-emulator.apk`.

## M1 — done
- **Simulation (pure C#, `Assets/Games/UrbanRunner/Scripts/Sim`):** 3 lanes, swipe and arrow-key input with a 0.15 s buffer and 0.12 s lane change, jump and slide, 4 obstacle types (crate, banner, stall/van, rolling cart), spark lines and arcs, 3 power-ups with upgrade-scaled timers, the speed ramp, crash, revive and run stats.
- **Generator:** a seeded PCG32 RNG picks from 25 hand-authored ScriptableObject chunks across 3 tiers. Gaps are placed in time, so every required action is ≥ 0.7 s apart. Tested over 100 seeds × 10,000 rows, plus extreme configs.
- **Autoplay bot** with a skill setting. At skill 1 it survives 5 minutes on 20 seeds in the sim, and 3 minutes in the real scene.
- **Session protocol (`RunSession`):** session.start/started/ended, the revive prompt → ad.request → ad.result (60 s timeout), pause and resume, quit, settings.update, and analytics forwarding.
- **Views:** pooled primitive visuals (no Instantiate mid-run, asserted by a test), floating origin every 500 m, chase camera with shake, and a TMP HUD (distance, sparks, power-up timers, pause menu, "Continue?" prompt).
- **Standalone host:** fakes Flutter in the Editor and in the standalone APK (autoplay loop, a forced crash every third session, fake ads).
- **Tests:** EditMode 79/79, PlayMode 5/5. The emulator standalone run was 300 s, SMOKE CLEAN (no crash markers), avg FPS about 52–54 (emulator, indicative), PSS 185 MB, and no GC during runs.
- **Evidence:** `release/screenshots/m1/`, `release/logs/m1_*`, `release/milestones/UrbanRunner-m1-standalone-emulator.apk`.
- **Fixed along the way (each with a regression test):** power-ups stopped spawning after the first; the Run scene was generated with null references; the batchmode watchdog aborted under `set -e`.

## Next
M2: Lantern City art pass, animation, camera, particles, audio mixer with music and SFX, haptics, tutorial, difficulty tuning, a performance pass (first-session hitch: `fps_min` 0.4 once, probably shader warm-up), and a zero per-frame allocation test.

## Watch list
- The APK is large: `libil2cpp.so` is about 65 MB per ABI uncompressed. Check stripping and the arm64 download size against the 80 MB budget in M2/M6.
- `tools/*.ps1` were written to match the `.sh` scripts but haven't been run, because the owner's machine is Linux and has no PowerShell.

## Blocked
Nothing. (Unity batchmode shutdown hang: worked around, see KNOWN_ISSUES K1.)
