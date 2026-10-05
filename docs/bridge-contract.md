# Bridge contract (v1)

> Copy of HANDOVER §6, kept in sync with the code. Unity: `unity/Assets/Core/Bridge/`. Flutter: `app/lib/core/bridge/`. If you change it, bump `v` on both sides and update the tests in `unity/Assets/Tests/EditMode/BridgeTests.cs` and `app/test/core/bridge/`.



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

## Implementation notes

- Unity: `BridgeEnvelope` (parse/serialize, Newtonsoft with the invariant culture), `BridgeRouter` (version check, dispatch, unknown types are logged and ignored, handler exceptions become `error` messages), `FlutterBridge` (the scene object named `FlutterBridge`, method `OnFlutterMessage`, sends `unity.ready` from `Start`), `IBridgeTransport` with `FlutterEmbedTransport` and `StandaloneHostTransport`.
- Flutter: `BridgeMessage` (encode/decode), `UnityBridge` (one per app, because Unity is loaded once and never unloaded, so `unity.ready` arrives only once; 10 s ready timeout with retry), `UnityTransport` with `EmbedUnityTransport`.
- Error codes Unity sends: `bad_message`, `version_mismatch`, `handler_failed`.

### Spike-only messages (M0, removed from the game scene in M1)

| Direction | type | payload |
| --- | --- | --- |
| Flutter → Unity | `spike.ping` | `id` |
| Unity → Flutter | `spike.pong` | `echo` (the ping payload), `frame` |
| Unity → Flutter | `spike.swipe` | `dir` (`left`/`right`/`up`/`down`) |
| Unity → Flutter | `spike.paused` / `spike.resumed` | – |
