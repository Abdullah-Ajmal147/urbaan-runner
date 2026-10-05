# Game Factory — Urban Runner

`HANDOVER.md` is the full spec. Read it completely before your first change, and re-read the relevant section before each milestone. This file is the short version you keep in mind at all times.

## What we're building
Game #1 of a mobile game factory: **Urban Runner**, an original modern endless runner set in "Lantern City". It's an Android app built from a **Flutter** shell with the menus, profile, missions, locker, store and settings, and an embedded **Unity 6** game that handles gameplay only. Firebase, AdMob and in-app purchases all live on the Flutter side. You build it, test it on the owner's Android emulator, and hand back the `release/` folder (HANDOVER §11).

## Repo map
- `unity/` — Unity 6 URP project. Shared code goes in `Assets/Core/`; game code goes in `Assets/Games/UrbanRunner/`.
- `app/` — Flutter. Shared code in `lib/core/`, screens in `lib/features/`, game data in `lib/games/urban_runner/`.
- `firebase/` — Firestore rules and rules tests.
- `tools/` — scripts: `env` (detects the Unity path), `test_all`, `build_all`, `emulator_smoke`. Each comes as `.ps1` and `.sh`.
- `docs/decisions.md`, `docs/bridge-contract.md`, `PROGRESS.md`, `ASSETS_LICENSES.md`.

## Commands
- All tests: `tools/test_all`
- Builds: `tools/build_all`
- Emulator smoke run: `tools/emulator_smoke`
- Raw commands and batchmode gotchas are in HANDOVER §8.3. The adb toolbox is in §10.2.

## Golden rules
1. Do the milestones in order (HANDOVER §9). A milestone is done only when its exit test passes. Then update `PROGRESS.md` and send the owner a five-line summary.
2. Unity is gameplay only. It never changes balances or saves the profile; it reports run facts over the bridge. Flutter owns the economy and applies rewards **once per sessionId**.
3. Every bridge message follows HANDOVER §6. If you change the contract, bump `v`, update both sides and update the tests.
4. Put anything reusable in Core. Game #2 must be able to reuse it.
5. Use original content only. Never use characters, names, art, sounds, music, levels or UI from existing games. Log every third-party asset in `ASSETS_LICENSES.md`.
6. Make reasonable decisions yourself and record them in `docs/decisions.md`. Ask the owner **only** about the items in HANDOVER §12.
7. Every bug fix gets a regression test. A flaky test is a bug. Never skip or delete a failing test to get green.
8. Close-the-Editor rule: never run Unity batchmode while the owner has this project open in the Unity Editor. Ask first if unsure.
9. Keep the log tags consistent: `UR_GAME`, `UR_BRIDGE`, `UR_PERF`, `UR_ERROR`.

## Never
- Commit secrets, keystores or `google-services.json`.
- Use real ad unit IDs in debug builds.
- Create accounts, spend money, accept terms on the owner's behalf, or publish anything.
- Edit `app/android/unityLibrary` by hand. Re-export instead.
- Ship the x86-64 (Chrome OS) architecture in store builds. It's for emulator builds only.

## Done means
- All tests are green.
- `tools/emulator_smoke` is clean.
- The `release/` folder is complete (HANDOVER §11.2).
- `TEST_REPORT.md` shows evidence for every check in HANDOVER §10.3 and §11.1.
