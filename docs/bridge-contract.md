# Bridge contract (v3)

> Copy of HANDOVER §6, kept in sync with the code. Unity: `unity/Assets/Core/Bridge/`. Flutter: `app/lib/core/bridge/`. If you change it, bump `v` on both sides and update the tests in `unity/Assets/Tests/EditMode/BridgeTests.cs` and `app/test/core/bridge/`.



**Transport.** Flutter calls `sendToUnity("FlutterBridge", "OnFlutterMessage", json)`. Unity replies with `SendToFlutter.Send(json)`. On the Unity side, wrap the transport behind an `IBridgeTransport` interface, so standalone Unity builds can use a `StandaloneHostTransport` that fakes Flutter.

**Envelope:**
```json
{ "v": 3, "type": "session.start", "sessionId": "uuid-v4", "payload": { } }
```

**Flutter → Unity**

| type | payload |
| --- | --- |
| `session.start` | `seed`, `mode` (`normal` / `daily` / `tutorial`), `character`, `trail`, `upgrades{magnet,shield,double}`, `config{start_speed,max_speed,speed_ramp_per_min,revives_per_run,revive_enabled}`, `settings{music,sfx,haptics,reduceMotion}`, `bestDistance` (v2: the player's best in metres, for the in-run "best" line; 0 if none), `hunt` (v3, optional: `{colour: 0–4, found: 0–4}`, the Lantern Hunt lantern to place this run; absent when today's hunt is complete or in the tutorial), optional `testMode{autoplay,skill(0–1),endAfterSeconds,forceCrashAtSeconds}` |
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
| `session.ended` | `mode`, `seed`, `distanceM`, `sparks`, `durationS`, `cause` (`obstacle` / `quit` / `test`), `revivesUsed`, `stats{jumps,slides,laneChanges,magnet,shield,doubleSparks}`, `tutorialCompleted` (v2: bool, true when a `tutorial` run finished every step), `huntFound` (v3: bool, this run's lantern was picked up), `buildVersion` |
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

## Implementation notes

- Unity: `BridgeEnvelope` (parse/serialize, Newtonsoft with the invariant culture), `BridgeRouter` (version check, dispatch, unknown types are logged and ignored, handler exceptions become `error` messages), `FlutterBridge` (the scene object named `FlutterBridge`, method `OnFlutterMessage`, sends `unity.ready` from `Start`), `IBridgeTransport` with `FlutterEmbedTransport` and `StandaloneHostTransport`.
- Flutter: `BridgeMessage` (encode/decode), `UnityBridge` (one per app, because Unity is loaded once and never unloaded, so `unity.ready` arrives only once; 10 s ready timeout with retry), `UnityTransport` with `EmbedUnityTransport`.
- Error codes Unity sends: `bad_message`, `version_mismatch`, `handler_failed`, `bad_session`, `session_active`.

### Version history

- **v1** (M0–M1): HANDOVER §6 as written.
- **v2** (M2): added `session.start.bestDistance` and `session.ended.tutorialCompleted`. Both sides reject other versions with `version_mismatch`. Tests: `BridgeTests.cs`, `app/test/core/bridge/bridge_test.dart`.
- **v3** (2026-10-06): Lantern Hunt. Flutter sends `session.start.hunt` and Unity places one lantern between 250 and 600 m, using its own random stream so the track itself is unchanged (the Daily Run stays identical for everyone). Unity reports `session.ended.huntFound`. Flutter counts it once per sessionId and pays the hunt prize when the 5th lantern of the day is found. Tests: `MovementTests.HuntLantern_*`, `SessionTests.Hunt_*`, `app/test/games/hunt_test.dart`, and the Lantern Hunt flow test.

### Unity behaviour details

- `session.start` needs a `sessionId`; without one Unity replies `error{code:"bad_session"}`. A `session.start` while a run is still active is rejected with `error{code:"session_active"}`. After `session.ended`, a new `session.start` can follow straight away, since Unity stays loaded.
- `seed` is an unsigned integer (a JSON number or a numeric string). The same seed and config always produce the same track.
- `config` values are clamped again on the Unity side to the §7.3 ranges (defence in depth). Flutter remains the owner of clamping and of logging `config_invalid`.
- Revive flow (HANDOVER §3.8): crash → Unity sends `analytics.event{name:"revive_offer"}` and shows "Continue?" for 5 s. Yes → `analytics.event{name:"revive_accept"}`, then `ad.request{placement:"revive"}`. No or timeout → `analytics.event{name:"revive_decline"}`, then `session.ended{cause:"obstacle"}`. In autoplay test mode, Unity accepts automatically after 0.5 s.
- `ad.result` is matched to the current session. Duplicates, late results and results for another session are ignored. Unity waits at most 60 s of **real** time (it still counts while the Unity player is paused for the ad).
- `analytics.event` params always include `session_id`.
- `session.ended`: `distanceM` is rounded to 0.1 m and `durationS` to 0.01 s. `durationS` is run time, excluding pauses and the revive prompt.
- **Sanity cap for Flutter (rule 3):** a run can't produce more than `1.2 sparks per metre` of `distanceM`, even with Double Sparks active the whole time (`TrackRules.MaxSparksPerMetre`, enforced by an EditMode test).
- Tutorial (`mode: "tutorial"`): four prompts (left, right, jump, slide) at 0.65× speed; before each scripted obstacle the run freezes until the right swipe, and tutorial obstacles can't hurt. Then "You're ready!" and the normal track continues in the same run. Flutter marks the tutorial done after the first tutorial run ends, whether or not every step was finished (the player is never forced through it twice; `tutorialCompleted` is logged for analytics). Tutorial runs never set a best distance.
- Backgrounding the app mid-run pauses the run (`OnApplicationPause`). Flutter sends `game.resume` when the player continues.

### Diagnostics (kept from the M0 spike)

| Direction | type | payload |
| --- | --- | --- |
| Flutter → Unity | `debug.ping` | anything (e.g. `id`) |
| Unity → Flutter | `debug.pong` | `echo` (the ping payload) |

The M0 spike used `spike.ping`/`spike.pong`/`spike.swipe`/`spike.paused`. Those were retired in M1 together with the spike scene.
