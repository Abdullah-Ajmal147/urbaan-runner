import 'package:flutter_test/flutter_test.dart';
import 'package:urban_runner/core/analytics/analytics.dart';
import 'package:urban_runner/core/remote_config/remote_config.dart';
import 'package:urban_runner/core/storage/local_store.dart';
import 'package:urban_runner/games/urban_runner/game_state.dart';
import 'package:urban_runner/games/urban_runner/streak.dart';

void main() {
  late MemoryLocalStore store;
  late DebugAnalytics analytics;
  var now = DateTime.utc(2026, 10, 5, 12);

  Future<GameState> boot() async {
    final config = RemoteConfig(UrConfig.specs, analytics: analytics);
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

  test('consecutive days grow the gift; day 7 is the big one, then the week starts again', () async {
    final s = await boot();
    final paid = <int>[];
    for (var d = 0; d < 9; d++) {
      now = DateTime.utc(2026, 10, 5 + d, 9);
      paid.add(await s.claimStreak());
    }
    expect(paid, [...DailyStreak.rewards, DailyStreak.rewards[0], DailyStreak.rewards[1]]);
    expect(s.profile.streak, 9);
    expect(s.profile.streakBest, 9);
  });

  test('missing a day resets the streak to day 1 (and says so)', () async {
    final s = await boot();
    for (var d = 0; d < 3; d++) {
      now = DateTime.utc(2026, 10, 5 + d, 9);
      await s.claimStreak();
    }
    now = DateTime.utc(2026, 10, 9, 9); // skipped the 8th
    final st = s.streakStatus;
    expect(st.day, 1);
    expect(st.broken, isTrue);
    expect(await s.claimStreak(), DailyStreak.rewards[0]);
    expect(s.profile.streak, 1);
    expect(s.profile.streakBest, 3, reason: 'the best streak is kept');
  });

  test('negative: double taps, a second claim and a restart on the same day pay once', () async {
    final s = await boot();
    final both = await Future.wait([s.claimStreak(), s.claimStreak()]);
    expect(both.where((r) => r > 0), hasLength(1));
    now = DateTime.utc(2026, 10, 5, 23, 59);
    expect(await s.claimStreak(), 0);
    final restarted = await boot();
    expect(restarted.streakStatus.claimedToday, isTrue);
    expect(await restarted.claimStreak(), 0);
    expect(restarted.profile.sparks, DailyStreak.rewards[0]);
    expect(analytics.named('streak_claim'), hasLength(1));
  });

  test('the day changes at UTC midnight, not after 24 hours', () async {
    final s = await boot();
    now = DateTime.utc(2026, 10, 5, 23, 50);
    await s.claimStreak();
    now = DateTime.utc(2026, 10, 6, 0, 5); // 15 minutes later, but a new day
    expect(s.streakStatus.claimedToday, isFalse);
    expect(s.streakStatus.day, 2);
  });
}
