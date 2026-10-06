import 'package:flutter_test/flutter_test.dart';
import 'package:urban_runner/core/analytics/analytics.dart';
import 'package:urban_runner/core/iap/store.dart';
import 'package:urban_runner/core/remote_config/remote_config.dart';
import 'package:urban_runner/core/storage/local_store.dart';
import 'package:urban_runner/games/urban_runner/game_state.dart';
import 'package:urban_runner/games/urban_runner/items.dart';
import 'package:urban_runner/games/urban_runner/run_result.dart';

RunResult run(String id, {double distance = 1000, int sparks = 100, String mode = 'normal', bool tutorial = false}) => RunResult(
    sessionId: id, mode: mode, seed: 1, distanceM: distance, sparks: sparks, durationS: 60, cause: 'obstacle', revivesUsed: 0, stats: const RunStats(), tutorialCompleted: tutorial);

void main() {
  late MemoryLocalStore store;
  late DebugAnalytics analytics;
  late RemoteConfig config;
  late GameState state;
  var now = DateTime.utc(2026, 10, 5, 12);

  Future<GameState> fresh({Map<String, Object?> rc = const {}}) async {
    config = RemoteConfig(UrConfig.specs, analytics: analytics);
    await config.activate(DefaultsOnlySource(rc));
    final s = GameState(store: store, analytics: analytics, config: config, clock: () => now);
    await s.load();
    return s;
  }

  setUp(() async {
    store = MemoryLocalStore();
    analytics = DebugAnalytics();
    now = DateTime.utc(2026, 10, 5, 12);
    state = await fresh();
  });

  test('a new player starts with Mira, the Neon Ribbon trail and 3 missions', () {
    final p = state.profile;
    expect(p.selectedCharacter, 'mira');
    expect(p.ownedItems, containsAll(['mira', 'trail_neon']));
    expect(p.missions, hasLength(3));
    expect(p.sparks, 0);
    expect(state.level, 1);
  });

  test('rewards are applied exactly once per sessionId (bridge rule 2)', () async {
    final first = await state.applyRun(run('s1', sparks: 100));
    final dup = await state.applyRun(run('s1', sparks: 100));
    expect(first.alreadyProcessed, isFalse);
    expect(dup.alreadyProcessed, isTrue);
    expect(state.profile.sparks, 100 + first.levelUpSparks);
    expect(state.profile.totalRuns, 1);
  });

  test('duplicate rewards survive an app restart too', () async {
    await state.applyRun(run('s1'));
    final again = await fresh();
    final dup = await again.applyRun(run('s1'));
    expect(dup.alreadyProcessed, isTrue);
    expect(again.profile.totalRuns, 1);
  });

  test('impossible sparks are capped and reported as result_rejected', () async {
    final r = await state.applyRun(run('s1', distance: 100, sparks: 50000));
    expect(r.rejected, isTrue);
    expect(r.sparksCredited, 125);
    expect(analytics.named('result_rejected').single.params['reason'], 'sparks_exceed_distance');
  });

  test('Remote Config multipliers apply at reward time only', () async {
    state = await fresh(rc: {'sparks_multiplier': 1.5, 'event_active': true, 'event_sparks_multiplier': 2.0});
    final r = await state.applyRun(run('s1', sparks: 100));
    expect(r.sparksCredited, 300);
  });

  test('new best is detected and saved; tutorials never count as a best', () async {
    final a = await state.applyRun(run('s1', distance: 800));
    final b = await state.applyRun(run('s2', distance: 500));
    final c = await state.applyRun(run('s3', distance: 1200));
    final t = await state.applyRun(run('s4', distance: 5000, mode: 'tutorial', tutorial: true));
    expect([a.newBest, b.newBest, c.newBest, t.newBest], [true, false, true, false]);
    expect(state.profile.bestDistance, 1200);
    expect(state.profile.tutorialDone, isTrue);
  });

  test('Daily Run best is per UTC day', () async {
    await state.applyRun(run('d1', distance: 900, mode: 'daily'));
    final worse = await state.applyRun(run('d2', distance: 700, mode: 'daily'));
    expect(state.todayDailyBest, 900);
    expect(worse.dailyBest, isFalse);
    now = DateTime.utc(2026, 10, 6, 1);
    expect(state.todayDailyBest, 0, reason: 'a new day starts fresh');
    final tomorrow = await state.applyRun(run('d3', distance: 300, mode: 'daily'));
    expect(tomorrow.dailyBest, isTrue);
    expect(analytics.named('daily_run_complete').length, 3);
  });

  test('XP levels you up, pays sparks and unlocks level items', () async {
    // Level 10 (Juno) needs 2,700 XP = 27 km; 30 km in one go also passes level 8 (Paper Kite).
    final r = await state.applyRun(run('big', distance: 30000, sparks: 0));
    expect(r.levelAfter, greaterThanOrEqualTo(10));
    expect(r.levelUpSparks, greaterThan(0));
    expect(r.unlocked, containsAll([Items.juno.id, Items.paperKite.id]));
    expect(state.isOwned(Items.juno.id), isTrue);
    expect(analytics.named('level_up').length, r.levelAfter - 1);
    expect(analytics.named('item_unlock').map((e) => e.params['source']).toSet(), {'level'});
  });

  test('"Double sparks" ad reward is granted once per run', () async {
    final r = await state.applyRun(run('s1', sparks: 80));
    final before = state.profile.sparks;
    expect(await state.grantDoubleReward(r), isTrue);
    expect(await state.grantDoubleReward(r), isFalse, reason: 'second callback ignored');
    expect(state.profile.sparks, before + 80);
  });

  test('"Double sparks" respects the Remote Config switch', () async {
    state = await fresh(rc: {'double_reward_enabled': false});
    final r = await state.applyRun(run('s1', sparks: 80));
    expect(await state.grantDoubleReward(r), isFalse);
  });

  test('claiming a mission pays out and rolls in a new one', () async {
    state.profile.missions[0].claimable = true;
    final oldId = state.profile.missions[0].templateId;
    final reward = await state.claimMission(0);
    expect(reward, greaterThan(0));
    expect(state.profile.sparks, greaterThanOrEqualTo(reward));
    expect(state.profile.missions[0].templateId, isNot(oldId));
    expect(state.profile.missions[0].claimable, isFalse);
    expect(await state.claimMission(0), 0, reason: 'nothing to claim twice');
    expect(analytics.named('mission_complete').single.params['mission_id'], oldId);
  });

  test('upgrades cost sparks, cap at level 5 and never go below zero', () async {
    expect(await state.buyUpgrade(PowerUp.magnet), isFalse, reason: 'no sparks yet');
    state.profile.sparks = 100000;
    for (var i = 0; i < 5; i++) {
      expect(await state.buyUpgrade(PowerUp.magnet), isTrue);
    }
    expect(await state.buyUpgrade(PowerUp.magnet), isFalse);
    expect(state.profile.upgrades['magnet'], 5);
    expect(state.profile.sparks, 100000 - Upgrades.costs.reduce((a, b) => a + b));
  });

  test('Dex can be bought with sparks and equipped; Juno cannot be bought', () async {
    state.profile.sparks = 2000;
    expect(await state.buyItem(Items.juno), isFalse);
    expect(await state.buyItem(Items.dex), isTrue);
    await state.equip(Items.dex);
    expect(state.profile.selectedCharacter, 'dex');
    expect(state.profile.sparks, 500);
    expect(await state.buyItem(Items.dex), isFalse, reason: 'already owned');
  });

  test('purchases are granted once per token (HANDOVER §7.5)', () async {
    const u = PurchaseUpdate(productId: Products.starterPack, status: PurchaseStatus.purchased, token: 't1');
    expect(await state.grantPurchase(u), isTrue);
    expect(await state.grantPurchase(u), isFalse);
    expect(state.profile.sparks, Products.starterSparks);
    expect(state.isOwned(Items.lantern.id), isTrue);
    expect(await state.grantPurchase(const PurchaseUpdate(productId: Products.removeAds, status: PurchaseStatus.restored, token: 't2')), isTrue);
    expect(state.profile.removeAds, isTrue);
    expect(await state.grantPurchase(const PurchaseUpdate(productId: Products.removeAds, status: PurchaseStatus.pending, token: 't3')), isFalse,
        reason: 'pending is not paid yet');
  });

  test('restoring the Starter Pack gives the trail back but not the sparks again', () async {
    expect(await state.grantPurchase(const PurchaseUpdate(productId: Products.starterPack, status: PurchaseStatus.restored, token: 'old')), isTrue);
    expect(state.isOwned(Items.lantern.id), isTrue);
    expect(state.profile.sparks, 0);
  });

  test('progress persists across a restart', () async {
    await state.applyRun(run('s1', distance: 1500, sparks: 300));
    state.profile.sparks += 0;
    final reloaded = await fresh();
    expect(reloaded.profile.bestDistance, 1500);
    expect(reloaded.profile.totalRuns, 1);
    expect(reloaded.profile.sparks, state.profile.sparks);
    expect(reloaded.profile.appSessions, 2);
  });

  test('reset clears progress but keeps purchases', () async {
    await state.applyRun(run('s1', distance: 5000, sparks: 500));
    await state.grantPurchase(const PurchaseUpdate(productId: Products.removeAds, status: PurchaseStatus.purchased, token: 'p1'));
    await state.grantPurchase(const PurchaseUpdate(productId: Products.starterPack, status: PurchaseStatus.purchased, token: 'p2'));
    await state.resetProgress();
    final p = state.profile;
    expect(p.sparks, 0);
    expect(p.bestDistance, 0);
    expect(state.level, 1);
    expect(p.removeAds, isTrue);
    expect(state.isOwned(Items.lantern.id), isTrue);
    expect(await state.grantPurchase(const PurchaseUpdate(productId: Products.starterPack, status: PurchaseStatus.restored, token: 'p2')), isFalse);
  });

  test('analytics: game_end carries the §7.2 params', () async {
    await state.applyRun(run('s9', distance: 1234.5, sparks: 77));
    final e = analytics.named('game_end').single.params;
    expect(e.keys, containsAll(['session_id', 'mode', 'distance_m', 'sparks', 'duration_s', 'cause', 'revives']));
    expect(e['distance_m'], 1234);
  });

  test('turning off "Share usage data" stops analytics', () async {
    await state.updateSettings(state.profile.settings.copyWith(shareUsageData: false));
    final count = analytics.events.length;
    await state.applyRun(run('s1'));
    expect(analytics.events.length, count);
  });
}
