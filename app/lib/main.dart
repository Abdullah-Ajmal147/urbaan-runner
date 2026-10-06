import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_embed_unity/flutter_embed_unity.dart';
import 'package:package_info_plus/package_info_plus.dart';

import 'app.dart';
import 'core/ads/debug_ads.dart';
import 'core/analytics/analytics.dart';
import 'core/app_services.dart';
import 'core/audio/ui_sounds.dart';
import 'core/bridge/unity_bridge.dart';
import 'core/bridge/unity_transport.dart';
import 'core/iap/store.dart';
import 'core/remote_config/remote_config.dart';
import 'core/storage/local_store.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await SystemChrome.setPreferredOrientations([DeviceOrientation.portraitUp]);
  final navigatorKey = GlobalKey<NavigatorState>();
  final analytics = DebugAnalytics();
  final info = await PackageInfo.fromPlatform();
  final services = AppServices(
    // One bridge for the whole app: Unity is loaded once and never unloaded (docs/decisions.md D8).
    bridge: UnityBridge(EmbedUnityTransport()),
    analytics: analytics,
    config: RemoteConfig(UrConfig.specs, analytics: analytics),
    ads: DebugAdsService(navigatorKey: navigatorKey),
    store: FakeStore(),
    sounds: UiSounds(),
    gameView: (context, bridge) => EmbedUnity(onMessageFromUnity: bridge.transport.deliver),
    buildVersion: '${info.version} (${info.buildNumber})',
  );
  runApp(UrbanRunnerApp(
    services: services,
    navigatorKey: navigatorKey,
    openStore: PrefsLocalStore.open,
    configSource: const DefaultsOnlySource(),
  ));
}
