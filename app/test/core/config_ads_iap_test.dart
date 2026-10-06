import 'package:flutter_test/flutter_test.dart';
import 'package:urban_runner/core/ads/ads.dart';
import 'package:urban_runner/core/analytics/analytics.dart';
import 'package:urban_runner/core/iap/store.dart';
import 'package:urban_runner/core/remote_config/remote_config.dart';
import 'package:urban_runner/core/storage/local_store.dart';
import 'package:urban_runner/features/store/purchases.dart';
import 'package:urban_runner/games/urban_runner/game_state.dart';
import 'package:urban_runner/games/urban_runner/items.dart';
import 'package:urban_runner/games/urban_runner/profile.dart';

void main() {
  group('Remote Config clamping (HANDOVER §7.3, §10.3 #8)', () {
    late DebugAnalytics analytics;
    late RemoteConfig rc;

    setUp(() {
      analytics = DebugAnalytics();
      rc = RemoteConfig(UrConfig.specs, analytics: analytics);
    });

    test('defaults when nothing was fetched', () async {
      await rc.activate(const DefaultsOnlySource());
      expect(rc.number('start_speed'), 8);
      expect(rc.number('max_speed'), 22);
      expect(rc.integer('revives_per_run'), 1);
      expect(rc.flag('revive_enabled'), isTrue);
      expect(rc.flag('event_active'), isFalse);
      expect(analytics.events, isEmpty);
    });

    test('valid values (including the range edges) are used as-is', () async {
      await rc.activate(const DefaultsOnlySource({'start_speed': 12, 'max_speed': 12, 'speed_ramp_per_min': 0.5, 'event_active': 'true', 'sparks_multiplier': '3.0'}));
      expect(rc.number('start_speed'), 12);
      expect(rc.number('max_speed'), 12);
      expect(rc.number('speed_ramp_per_min'), 0.5);
      expect(rc.flag('event_active'), isTrue);
      expect(rc.number('sparks_multiplier'), 3.0);
      expect(analytics.events, isEmpty);
    });

    test('out-of-range values are clamped and logged as config_invalid', () async {
      await rc.activate(const DefaultsOnlySource({'start_speed': 99, 'revives_per_run': -3, 'interstitial_min_interval_s': 5}));
      expect(rc.number('start_speed'), 12);
      expect(rc.integer('revives_per_run'), 0);
      expect(rc.integer('interstitial_min_interval_s'), 60);
      expect(analytics.named('config_invalid').map((e) => e.params['key']), containsAll(['start_speed', 'revives_per_run', 'interstitial_min_interval_s']));
    });

    test('garbage values fall back to the default and are logged', () async {
      await rc.activate(const DefaultsOnlySource({'max_speed': 'fast', 'revive_enabled': 'maybe', 'sparks_multiplier': double.nan}));
      expect(rc.number('max_speed'), 22);
      expect(rc.flag('revive_enabled'), isTrue);
      expect(rc.number('sparks_multiplier'), 1.0);
      expect(analytics.named('config_invalid').length, 3);
    });
  });

  group('Interstitial policy, simulated over runs and time (HANDOVER §7.4)', () {
    const policy = InterstitialPolicy(firstAfterRuns: 3, everyNRuns: 3, minIntervalS: 120);

    /// Plays [runs] runs, each [runSeconds] long, returning the run numbers after which an ad was shown.
    List<int> simulate({int runs = 20, int runSeconds = 60, bool removeAds = false, Set<int> rewardedAfter = const {}}) {
      final shown = <int>[];
      var lastShown = 0;
      var sinceLast = 0;
      var now = 1000000;
      for (var r = 1; r <= runs; r++) {
        now += runSeconds * 1000;
        sinceLast++;
        final show = policy.shouldShow(
          removeAds: removeAds,
          firstAppSession: false,
          tutorialRun: r == 1,
          justWatchedRewarded: rewardedAfter.contains(r),
          totalRuns: r,
          runsSinceLast: sinceLast,
          lastShownMs: lastShown,
          nowMs: now,
        );
        if (show) {
          shown.add(r);
          lastShown = now;
          sinceLast = 0;
        }
      }
      return shown;
    }

    test('never before run 3, then at most every 3 runs', () {
      expect(simulate(), [3, 6, 9, 12, 15, 18]);
    });

    test('the time cooldown also applies: short runs push ads further apart', () {
      // 20-second runs: 3 runs is only 60 s, so the 120 s cooldown wins.
      final shown = simulate(runSeconds: 20);
      for (var i = 1; i < shown.length; i++) {
        expect((shown[i] - shown[i - 1]) * 20, greaterThanOrEqualTo(120));
      }
      expect(shown.first, 3);
    });

    test('never right after a rewarded ad', () {
      expect(simulate(rewardedAfter: {3}), [4, 7, 10, 13, 16, 19]);
    });

    test('never with Remove Ads, never in the first session or the tutorial', () {
      expect(simulate(removeAds: true), isEmpty);
      expect(policy.shouldShow(removeAds: false, firstAppSession: true, tutorialRun: false, justWatchedRewarded: false, totalRuns: 10, runsSinceLast: 10, lastShownMs: 0, nowMs: 999999999), isFalse);
      expect(policy.shouldShow(removeAds: false, firstAppSession: false, tutorialRun: true, justWatchedRewarded: false, totalRuns: 10, runsSinceLast: 10, lastShownMs: 0, nowMs: 999999999), isFalse);
    });
  });

  group('Purchases state machine with FakeStore (HANDOVER §7.5)', () {
    late FakeStore store;
    late GameState state;
    late Purchases purchases;
    late List<String> messages;
    late DebugAnalytics analytics;

    setUp(() async {
      store = FakeStore();
      analytics = DebugAnalytics();
      final rc = RemoteConfig(UrConfig.specs);
      state = GameState(store: MemoryLocalStore(), analytics: analytics, config: rc, clock: () => DateTime.utc(2026, 10, 5));
      await state.load();
      messages = [];
      purchases = Purchases(store: store, state: state, analytics: analytics)..notify = messages.add;
    });

    Future<void> settle() => Future<void>.delayed(const Duration(milliseconds: 10));

    test('buy → granted once, acknowledged, analytics complete', () async {
      await purchases.buy(Products.removeAds, consumable: false);
      await settle();
      expect(state.profile.removeAds, isTrue);
      expect(store.completed, hasLength(1));
      expect(analytics.named('iap_start'), hasLength(1));
      expect(analytics.named('iap_complete'), hasLength(1));
      expect(purchases.busy, isFalse);
    });

    test('a duplicate store callback grants nothing new but is acknowledged again', () async {
      store.nextResult = PurchaseStatus.purchased;
      await purchases.buy(Products.starterPack, consumable: false);
      await settle();
      final sparks = state.profile.sparks;
      store.redeliver(PurchaseUpdate(productId: Products.starterPack, status: PurchaseStatus.purchased, token: store.owned[Products.starterPack]));
      await settle();
      expect(state.profile.sparks, sparks);
      expect(store.completed, hasLength(2));
      expect(analytics.named('iap_complete'), hasLength(1));
    });

    test('cancel grants nothing and logs iap_cancel', () async {
      store.nextResult = PurchaseStatus.canceled;
      await purchases.buy(Products.starterPack, consumable: false);
      await settle();
      expect(state.profile.sparks, 0);
      expect(analytics.named('iap_cancel'), hasLength(1));
      expect(purchases.busy, isFalse);
    });

    test('pending waits, then grants when the payment clears', () async {
      store.nextResult = PurchaseStatus.pending;
      await purchases.buy(Products.starterPack, consumable: false);
      await settle();
      expect(state.profile.sparks, 0);
      expect(purchases.pending, contains(Products.starterPack));
      expect(messages.last, contains('Waiting'));
      store.resolvePending(Products.starterPack, 'late-token');
      await settle();
      expect(state.profile.sparks, Products.starterSparks);
      expect(state.isOwned(Items.lantern.id), isTrue);
      expect(purchases.pending, isEmpty);
    });

    test('error grants nothing and tells the player nothing was charged', () async {
      store.nextResult = PurchaseStatus.error;
      await purchases.buy(Products.removeAds, consumable: false);
      await settle();
      expect(state.profile.removeAds, isFalse);
      expect(analytics.named('iap_fail'), hasLength(1));
      expect(messages.last, contains('Nothing was charged'));
    });

    test('restore brings back Remove Ads on a fresh install', () async {
      await purchases.buy(Products.removeAds, consumable: false);
      await settle();
      // Same store account, new install:
      final fresh = GameState(store: MemoryLocalStore(), analytics: analytics, config: RemoteConfig(UrConfig.specs), clock: () => DateTime.utc(2026, 10, 5));
      await fresh.load();
      final p2 = Purchases(store: store, state: fresh, analytics: analytics);
      await p2.restore(quiet: true);
      await settle();
      expect(fresh.profile.removeAds, isTrue);
    });
  });

  group('Profile merge rules (HANDOVER §7.1)', () {
    test('max for growing fields, union for owned items, latest write for selections', () {
      final local = PlayerProfile(createdAt: DateTime.utc(2026, 1, 1), updatedAt: DateTime.utc(2026, 10, 5, 10), bestDistance: 900, totalRuns: 10, xp: 500, sparks: 40, selectedCharacter: 'dex', ownedItems: {'mira', 'trail_neon', 'dex'});
      final remote = PlayerProfile(createdAt: DateTime.utc(2025, 12, 1), updatedAt: DateTime.utc(2026, 10, 5, 12), bestDistance: 1200, totalRuns: 8, xp: 450, sparks: 70, selectedCharacter: 'mira', ownedItems: {'mira', 'trail_neon', 'trail_kite'});
      final m = local.mergedWith(remote);
      expect(m.bestDistance, 1200);
      expect(m.totalRuns, 10);
      expect(m.xp, 500);
      expect(m.ownedItems, {'mira', 'trail_neon', 'dex', 'trail_kite'});
      expect(m.selectedCharacter, 'mira', reason: 'remote was written later');
      expect(m.sparks, 70);
      expect(m.createdAt, DateTime.utc(2025, 12, 1));
    });
  });
}
