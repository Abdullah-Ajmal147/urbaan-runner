import 'package:flutter/widgets.dart';

import 'ads/ads.dart';
import 'analytics/analytics.dart';
import 'audio/ui_sounds.dart';
import 'bridge/unity_bridge.dart';
import 'iap/store.dart';
import 'remote_config/remote_config.dart';

/// Builds the widget that shows the game: the real EmbedUnity view, or a stand-in in tests.
typedef GameViewBuilder = Widget Function(BuildContext context, UnityBridge bridge);

/// App-wide services, created once in main() and provided to the widget tree.
class AppServices {
  AppServices({
    required this.bridge,
    required this.analytics,
    required this.config,
    required this.ads,
    required this.store,
    required this.sounds,
    required this.gameView,
    required this.buildVersion,
  });

  final UnityBridge bridge;
  final Analytics analytics;
  final RemoteConfig config;
  final AdsService ads;
  final Store store;
  final UiSounds sounds;
  final GameViewBuilder gameView;
  final String buildVersion;
}
