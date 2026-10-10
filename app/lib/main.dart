import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:package_info_plus/package_info_plus.dart';

import 'app.dart';
import 'core/ads/admob_ads.dart';
import 'core/analytics/analytics.dart';
import 'core/app_services.dart';
import 'core/audio/ui_sounds.dart';
import 'core/bridge/unity_bridge.dart';
import 'core/bridge/unity_transport.dart';
import 'core/firebase/firebase_services.dart';
import 'core/iap/play_store.dart';
import 'core/remote_config/remote_config.dart';
import 'core/storage/local_store.dart';
import 'core/unity/unity_game_view.dart';

Future<void> main() => runUrbanRunner(testMode: autoplayFromDefines());

/// `--dart-define=UR_AUTOPLAY=<seconds>` (debug/profile only): every run is played by Unity's bot and ends
/// after that many seconds. Used by `tools/emulator_smoke` and the combined integration test.
Map<String, Object?>? autoplayFromDefines() {
  const seconds = int.fromEnvironment('UR_AUTOPLAY');
  if (kReleaseMode || seconds <= 0) return null;
  return {'autoplay': true, 'skill': 1.0, 'endAfterSeconds': seconds};
}

Future<void> runUrbanRunner({Map<String, Object?>? testMode}) async {
  WidgetsFlutterBinding.ensureInitialized();
  await SystemChrome.setPreferredOrientations([DeviceOrientation.portraitUp]);
  final navigatorKey = GlobalKey<NavigatorState>();
  // Firebase is optional (D26/D34): without google-services.json, or if init fails, the game runs local-only.
  final firebase = await FirebaseSetup.init();
  final Analytics analytics = firebase == FirebaseMode.real ? FirebaseAnalyticsSink() : DebugAnalytics();
  final info = await PackageInfo.fromPlatform();
  final services = AppServices(
    // One bridge for the whole app: Unity is loaded once and never unloaded (docs/decisions.md D8).
    bridge: UnityBridge(EmbedUnityTransport()),
    analytics: analytics,
    config: RemoteConfig(UrConfig.specs, analytics: analytics),
    // Google Mobile Ads with Google's test units (real units only via release --dart-define, owner task 5)
    ads: AdMobAdsService(),
    // Play Billing; until the products exist in Play Console the Store screen shows them as unavailable
    store: PlayStore(),
    sounds: UiSounds(),
    gameView: (context, bridge) => UnityGameView(onMessageFromUnity: bridge.transport.deliver),
    buildVersion: '${info.version} (${info.buildNumber})',
    runTestMode: kReleaseMode ? null : testMode,
    cloud: firebase == FirebaseMode.off ? null : FirestoreBackend(),
  );
  runApp(UrbanRunnerApp(
    services: services,
    navigatorKey: navigatorKey,
    openStore: PrefsLocalStore.open,
    configSource: firebase == FirebaseMode.real ? FirebaseRemoteConfigSource() : const DefaultsOnlySource(),
  ));
}
