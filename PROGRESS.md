# Progress — Urban Runner

| Milestone | Status | Exit test |
| --- | --- | --- |
| M0 Environment, repo, bridge spike | ✅ done 2026-10-05 | test_all green; spike 7/7 on emulator (docs/decisions.md D10) |
| M1 Core gameplay (Unity standalone) | ⏳ next | – |
| M2 Feel, polish, original look | – | – |
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

## Next
M1: player controller, seeded chunk generator, obstacles/sparks/power-ups, speed ramp, crash and revive hook, HUD, pause, pooling, floating origin, autoplay bot, standalone host.

## Watch list
- The APK is large: `libil2cpp.so` is about 65 MB per ABI uncompressed. Check stripping and the arm64 download size against the 80 MB budget in M2/M6.
- `tools/*.ps1` were written to match the `.sh` scripts but haven't been run, because the owner's machine is Linux and has no PowerShell.

## Blocked
Nothing. (Unity batchmode shutdown hang: worked around, see KNOWN_ISSUES K1.)
