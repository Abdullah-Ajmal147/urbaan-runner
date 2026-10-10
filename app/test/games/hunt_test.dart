import 'package:flutter_test/flutter_test.dart';
import 'package:urban_runner/core/analytics/analytics.dart';
import 'package:urban_runner/core/remote_config/remote_config.dart';
import 'package:urban_runner/core/storage/local_store.dart';
import 'package:urban_runner/games/urban_runner/game_state.dart';
import 'package:urban_runner/games/urban_runner/hunt.dart';
import 'package:urban_runner/games/urban_runner/run_result.dart';
import 'package:urban_runner/games/urban_runner/session_payload.dart';

RunResult run(String id, {bool hunt = true, String mode = 'normal'}) => RunResult(
    sessionId: id, mode: mode, seed: 1, distanceM: 500, sparks: 20, durationS: 40, cause: 'obstacle', revivesUsed: 0, stats: const RunStats(), huntFound: hunt);

void main() {
  late MemoryLocalStore store;
  late DebugAnalytics analytics;
  late RemoteConfig config;
  var now = DateTime.utc(2026, 10, 5, 12);

  Future<GameState> boot() async {
    config = RemoteConfig(UrConfig.specs, analytics: analytics);
    await config.activate(const DefaultsOnlySource());
    final s = GameState(store: store, analytics: analytics, config: config, clock: () => now);
    await s.load();
    return s;
  }

  setUp(() {
    store = MemoryLocalStore();
    analytics = DebugAnalytics();
    now = DateTime.utc(2026, 10, 5, 12);
  });

  test('regression: no revive offer (it is "watch a short ad") in the tutorial or the first app session', () async {
    final s = await boot(); // load() counts this as app session 1
    Map<String, Object?> cfg(String mode) =>
        SessionPayload.build(profile: s.profile, config: config, mode: mode, seed: 1, today: s.todayKey)['config'] as Map<String, Object?>;
    expect(cfg('tutorial')['revive_enabled'], isFalse);
    expect(cfg('normal')['revive_enabled'], isFalse, reason: 'first session: no ads at all (§7.4)');
    final s2 = await boot(); // the next app start
    expect(s2.profile.appSessions, 2);
    Map<String, Object?> cfg2(String mode) =>
        SessionPayload.build(profile: s2.profile, config: config, mode: mode, seed: 1, today: s2.todayKey)['config'] as Map<String, Object?>;
    expect(cfg2('normal')['revive_enabled'], isTrue);
    expect(cfg2('tutorial')['revive_enabled'], isFalse);
  });

  test('session.start asks Unity for the next colour, never in the tutorial, none once the hunt is done', () async {
    final s = await boot();
    Map<String, Object?> payload(String mode) =>
        SessionPayload.build(profile: s.profile, config: config, mode: mode, seed: 1, today: s.todayKey);
    expect(payload('normal')['hunt'], {'colour': 0, 'found': 0});
    expect(payload('tutorial').containsKey('hunt'), isFalse);
    for (var i = 0; i < 5; i++) {
      await s.applyRun(run('r$i'));
    }
    expect(payload('normal').containsKey('hunt'), isFalse, reason: "today's hunt is complete");
  });

  test('five lanterns complete the hunt and pay the prize once; extra lanterns and duplicates count nothing', () async {
    final s = await boot();
    final sparksBefore = s.profile.sparks;
    for (var i = 0; i < 4; i++) {
      final r = await s.applyRun(run('a$i'));
      expect(r.huntFoundNow, isTrue);
      expect(r.huntPrize, 0);
    }
    final last = await s.applyRun(run('a4'));
    expect(last.huntFoundToday, 5);
    expect(last.huntPrize, LanternHunt.prizeFor(1));
    expect((await s.applyRun(run('a4'))).huntPrize, 0, reason: 'duplicate session.ended');
    expect((await s.applyRun(run('a5'))).huntFoundNow, isFalse, reason: 'already complete today');
    expect(analytics.named('hunt_complete'), hasLength(1));
    expect(s.profile.sparks - sparksBefore, greaterThanOrEqualTo(6 * 20 + LanternHunt.prizeFor(1)), reason: 'run sparks plus the prize (plus any level-up gifts)');
  });

  test('progress resets at UTC midnight; completing on consecutive days grows the prize, a gap resets it', () async {
    final s = await boot();
    Future<int> completeDay(int day) async {
      now = DateTime.utc(2026, 10, day, 10);
      var prize = 0;
      for (var i = 0; i < 5; i++) {
        prize += (await s.applyRun(run('d$day-$i'))).huntPrize;
      }
      return prize;
    }

    expect(await completeDay(5), LanternHunt.prizeFor(1));
    now = DateTime.utc(2026, 10, 6, 0, 1);
    expect(s.huntFoundToday, 0, reason: 'a new day, a new hunt');
    expect(await completeDay(6), LanternHunt.prizeFor(2));
    expect(s.huntStreakNow, 2);
    expect(await completeDay(8), LanternHunt.prizeFor(1), reason: 'missed the 7th');
  });

  test('a run without the lantern, or a tutorial, changes nothing', () async {
    final s = await boot();
    await s.applyRun(run('x', hunt: false));
    await s.applyRun(run('t', mode: 'tutorial'));
    expect(s.huntFoundToday, 0);
  });

  test('hunt progress survives a restart', () async {
    final s = await boot();
    await s.applyRun(run('p1'));
    await s.applyRun(run('p2'));
    final again = await boot();
    expect(again.huntFoundToday, 2);
  });
}
