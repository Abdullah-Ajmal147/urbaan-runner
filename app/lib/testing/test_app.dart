import 'package:flutter/material.dart';

import '../app.dart';
import '../core/ads/debug_ads.dart';
import '../core/analytics/analytics.dart';
import '../core/app_services.dart';
import '../core/audio/ui_sounds.dart';
import '../core/bridge/unity_bridge.dart';
import '../core/iap/store.dart';
import '../core/remote_config/remote_config.dart';
import '../core/storage/local_store.dart';
import '../features/home/streak_sheet.dart';
import 'fake_unity_game.dart';

/// Builds the full app around a [FakeUnityGame] for widget and integration tests.
/// Keep [store] between two harnesses to simulate an app restart.
class TestHarness {
  TestHarness({LocalStore? store, FakeUnityGame? game, this.config = const {}, DateTime Function()? clock})
      : store = store ?? MemoryLocalStore(),
        game = game ?? FakeUnityGame(),
        clock = clock ?? (() => DateTime.utc(2026, 10, 5, 12)) {
    StreakSheet.offeredThisSession = false; // each harness is a fresh app process
  }

  final LocalStore store;
  final FakeUnityGame game;
  final Map<String, Object?> config;
  final DateTime Function() clock;
  final navigatorKey = GlobalKey<NavigatorState>();
  final analytics = DebugAnalytics();
  final iap = FakeStore();
  late final ads = DebugAdsService(navigatorKey: navigatorKey, adSeconds: 1);
  late final services = AppServices(
    bridge: UnityBridge(game),
    analytics: analytics,
    config: RemoteConfig(UrConfig.specs, analytics: analytics),
    ads: ads,
    store: iap,
    sounds: UiSounds(enabled: false),
    gameView: (context, bridge) {
      game.attach();
      return const ColoredBox(color: Color(0xFF241A5C), child: Center(child: Text('FAKE GAME', key: Key('fake_game'))));
    },
    buildVersion: '0.1.0 (test)',
  );

  Widget app() => UrbanRunnerApp(
        services: services,
        navigatorKey: navigatorKey,
        openStore: () async => store,
        configSource: DefaultsOnlySource(config),
        clock: clock,
        splashMinimum: Duration.zero,
      );
}
