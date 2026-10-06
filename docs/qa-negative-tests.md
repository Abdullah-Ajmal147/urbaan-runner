# Negative test pass — 2026-10-06

Requested by the owner ("do detail test like negative test"). Run on the emulator (`ur_api36`, API 36, x86_64) with
the full app (Flutter + embedded Unity), plus host tests. Evidence (screenshots, logcat, monkey output, ANR trace)
is in `build/qa/` and goes into `release/` with the hand-back.

## Results

| # | Negative case | Result | Issue / fix |
| --- | --- | --- | --- |
| N1 | Back button during a run | **FAIL → fixed** | I2: Android sends Back to Unity's view, Flutter never saw it. Unity now handles it: run → pause → resume; on "Keep running?" → no thanks. `SessionTests.Back_*` |
| N2 | Home button mid-run → reopen | **PASS** (paused) + **bug** | I3: tutorial prompt drawn over the Pause menu. Hidden while paused. `RunSceneTests.TutorialPrompt_HidesWhilePaused` |
| N3 | Kill app mid-run → reopen | PASS | Run lost (normal), nothing half-applied, no crash |
| N3b | Kill app while Unity loads for the first time → reopen | PASS | Recovers and plays |
| N4 | Kill app on Results | PASS | 12 sparks earned → 12 after relaunch (once, persisted) |
| N5 | Airplane mode from first launch, play | PASS | Fully playable offline |
| N6 | Triple-tap Play | PASS | One game, one `session.start` |
| N7 | Rotate mid-run | PASS | Stays portrait, run continues |
| N8 | Monkey 15,000 random events (3 seeds) | 1 ANR, **not reproducible** | I7: one ANR inside Unity's own volume listener while the app was being frozen in the background; replaying the same seed and another seed: 0 ANR, 0 crash. Logged in KNOWN_ISSUES (watch) |
| N9 | Back on Store/Locker/Missions tab | **FAIL → fixed** | I11: closed the app. Now returns to Home; Back on Home exits. Flow test |
| N10 | Buy without enough sparks / twice / at max level / locked equip | PASS | New unit tests (double-tap buy, locked equip, upgrade at max) |
| N11 | Double-tap Claim | PASS | New unit test: paid once |
| N12 | Revive ad fails | PASS | New flow test: `granted:false`, Unity resumed, Results shown |
| N13 | Purchase cancel / pending / error / duplicate / restore | PASS | Existing tests |
| N14 | Bad Remote Config values | PASS | Existing tests (clamped, defaults, `config_invalid`) |
| N15 | Biggest system font | PASS (capped 1.3×) + **design bug** | I4: Home characters covered the "Urban Runner" title and subtitle (also at normal size). Image now fits above the text band |
| N17 | Clear app data | PASS | Fresh start, no crash |
| N18 | Device date +27 h while app in background | **FAIL → fixed** | I12: Home still said "11h left" (yesterday's Daily). Home refreshes on resume and every minute. Flow test |
| N19 | Malformed `session.ended` (wrong types, NaN, huge numbers) | **FAIL → fixed** | I10: `NaN.toInt()` crashed reward processing → player stuck on the game screen. Parser never throws, values clamped. `run_result_test.dart` + unit test |
| N20 | Tutorial results | **bug → fixed** | I13: showed "Best 244 m" though lessons never count as a best; "0 min 28 s". Now "Lesson done · 28 s". Flow test |
| – | Screen left on Home / Results | **bug → fixed** | I8: endless float/confetti animations redrew the screen 60×/s forever (battery). Now a few loops then rest. Flow test (screen must settle) |
| – | In-game: Warden drone at the start | **bug → fixed** | I1: the drone hovered over the next obstacles and hid them. Now flies low behind the courier |
| – | Unity "Not enough storage space" dialog | seen once | I5: only after the emulator itself crashed mid-install; clean reinstall fine. Watch |
| N16 | Low-spec phone (2 GB, 720p) | pending | Next |

Fixed: 10 issues, each with a regression test. Not reproducible: 2 (logged).
