import 'package:flutter_test/flutter_test.dart';
import 'package:urban_runner/core/analytics/analytics.dart';
import 'package:urban_runner/core/remote_config/remote_config.dart';
import 'package:urban_runner/core/storage/local_store.dart';
import 'package:urban_runner/core/sync/cloud_sync.dart';
import 'package:urban_runner/games/urban_runner/game_state.dart';
import 'package:urban_runner/games/urban_runner/items.dart';
import 'package:urban_runner/games/urban_runner/profile_sync.dart';
import 'package:urban_runner/games/urban_runner/profile.dart';
import 'package:urban_runner/games/urban_runner/run_result.dart';

RunResult run(String id, {double distance = 1000, int sparks = 100}) => RunResult(
    sessionId: id, mode: 'normal', seed: 1, distanceM: distance, sparks: sparks, durationS: 60, cause: 'obstacle', revivesUsed: 0, stats: const RunStats());

/// The same checks as firebase/firestore.rules, so the sync code is tested against what the server enforces.
String? firestoreLikeRules(String path, Map<String, Object?>? before, Map<String, Object?> after) {
  if (path.startsWith('users/')) {
    final rise = (after['sparks'] as int) - ((before?['sparks'] as int?) ?? 0);
    if (rise > UrbanRunnerSyncAdapter.maxSparksRisePerWrite) return 'sparks rose by $rise';
    if (before != null && before['createdAt'] != after['createdAt']) return 'createdAt changed';
    if ((after['level'] as int) < 1) return 'level';
  } else {
    if (before != null && (after['bestDistance'] as num) < (before['bestDistance'] as num)) return 'bestDistance decreased';
    if (before != null && (after['totalRuns'] as int) < (before['totalRuns'] as int)) return 'totalRuns decreased';
  }
  return null;
}

class Device {
  Device(this.cloud, this.now);
  final MemoryCloudBackend cloud;
  DateTime now;
  final store = MemoryLocalStore();
  late GameState state;
  late CloudSync<PlayerProfile> sync;

  Future<Device> boot() async {
    final analytics = DebugAnalytics();
    final config = RemoteConfig(UrConfig.specs, analytics: analytics);
    await config.activate(const DefaultsOnlySource());
    state = GameState(store: store, analytics: analytics, config: config, clock: () => now);
    await state.load();
    sync = CloudSync<PlayerProfile>(
      backend: cloud,
      adapter: UrbanRunnerSyncAdapter(),
      readLocal: () => state.profile,
      applyMerged: state.replaceProfile,
      debounce: const Duration(milliseconds: 10),
      retryAfter: const Duration(hours: 1),
    );
    state.onSaved = (_) => sync.schedule();
    return this;
  }
}

void main() {
  late MemoryCloudBackend cloud;
  final t0 = DateTime.utc(2026, 10, 5, 12);

  setUp(() => cloud = MemoryCloudBackend()..rules = firestoreLikeRules);

  test('first sync uploads both documents in the §7.1 layout', () async {
    final a = await Device(cloud, t0).boot();
    await a.state.applyRun(run('s1', distance: 1450, sparks: 210));
    expect(await a.sync.syncNow(), isTrue);
    final user = cloud.docs['users/test-uid']!;
    final player = cloud.docs['games/urban_runner/players/test-uid']!;
    expect(user['sparks'], a.state.profile.sparks);
    expect(user['level'], greaterThanOrEqualTo(1));
    expect(user['createdAt'], isA<DateTime>());
    expect(player['bestDistance'], 1450);
    expect(player['totalRuns'], 1);
    expect(user.containsKey('processedSessions'), isFalse, reason: 'device-only fields stay local');
    expect(a.sync.status, SyncStatus.synced);
  });

  test('a second device merges: max for best/runs/xp, union for owned items, newest selection', () async {
    final a = await Device(cloud, t0).boot();
    await a.state.applyRun(run('a1', distance: 2000, sparks: 300));
    await a.sync.syncNow();

    final b = await Device(cloud, t0.add(const Duration(minutes: 5))).boot();
    await b.state.applyRun(run('b1', distance: 900, sparks: 50));
    await b.state.equip(Items.trails.first);
    await b.sync.syncNow();

    final p = b.state.profile;
    expect(p.bestDistance, 2000, reason: 'max of 2000 and 900');
    expect(p.processedSessions, contains('b1'));
    expect(cloud.docs['games/urban_runner/players/test-uid']!['bestDistance'], 2000);
    expect(cloud.docs['games/urban_runner/players/test-uid']!['totalRuns'], 1, reason: 'max, not sum (no server to count runs)');

    // Device A picks up B's newer selection on its next sync.
    a.now = t0.add(const Duration(minutes: 10));
    await a.sync.syncNow();
    expect(a.state.profile.selectedTrail, b.state.profile.selectedTrail);
  });

  test('offline first launch → play → reconnect leaves the profile synced (M4 exit)', () async {
    cloud.online = false;
    final a = await Device(cloud, t0).boot();
    expect(await a.sync.syncNow(), isFalse);
    expect(a.sync.status, SyncStatus.offline);
    await a.state.applyRun(run('off1', distance: 1200, sparks: 150));
    await a.state.applyRun(run('off2', distance: 800, sparks: 90));
    expect(cloud.docs, isEmpty);

    cloud.online = true;
    expect(await a.sync.syncNow(), isTrue);
    expect(cloud.docs['games/urban_runner/players/test-uid']!['totalRuns'], 2);
    expect(cloud.docs['games/urban_runner/players/test-uid']!['bestDistance'], 1200);
    expect(cloud.docs['users/test-uid']!['sparks'], a.state.profile.sparks);
  });

  test('a big offline balance is uploaded in steps the rules accept (≤ 20,000 per write)', () async {
    final a = await Device(cloud, t0).boot();
    a.state.profile.sparks = 50000; // e.g. a large pack bought while offline
    await a.sync.syncNow();
    expect(cloud.docs['users/test-uid']!['sparks'], 20000);
    await a.sync.syncNow();
    expect(cloud.docs['users/test-uid']!['sparks'], 40000);
    expect(await a.sync.syncNow(), isTrue);
    expect(cloud.docs['users/test-uid']!['sparks'], 50000);
    expect(a.state.profile.sparks, 50000, reason: 'the device balance is never reduced by the upload cap');
  });

  test('saves while syncing are debounced into one more sync, and applying a merge does not loop', () async {
    final a = await Device(cloud, t0).boot();
    await a.sync.syncNow();
    final writes = cloud.writes;
    await a.state.applyRun(run('x1'));
    await a.state.applyRun(run('x2'));
    await Future<void>.delayed(const Duration(milliseconds: 50));
    expect(cloud.writes, writes + 1, reason: 'two saves → one debounced sync');
    await Future<void>.delayed(const Duration(milliseconds: 100));
    expect(cloud.writes, writes + 1, reason: 'the merge it applied did not schedule another sync');
    a.sync.dispose();
  });
}
