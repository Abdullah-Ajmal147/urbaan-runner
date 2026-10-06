import '../log.dart';

/// Analytics behind an interface (HANDOVER §7.1): a debug sink until real Firebase is configured.
/// Event names: snake_case, ≤ 40 chars, never a Firebase reserved name (§7.2).
abstract class Analytics {
  /// Off when the player turns "Share usage data" off in Settings.
  bool get enabled;
  set enabled(bool value);

  void log(String name, [Map<String, Object> params = const {}]);
}

class AnalyticsEvent {
  const AnalyticsEvent(this.name, this.params);
  final String name;
  final Map<String, Object> params;
  @override
  String toString() => '$name $params';
}

/// Records events in memory and to logcat (UR_GAME). Tests assert on [events].
class DebugAnalytics implements Analytics {
  static final _validName = RegExp(r'^[a-z][a-z0-9_]{0,39}$');
  static const _reserved = {'first_open', 'session_start', 'screen_view', 'app_remove', 'app_update', 'error', 'user_engagement', 'ad_impression', 'ad_click', 'in_app_purchase', 'notification_open'};

  final events = <AnalyticsEvent>[];

  @override
  bool enabled = true;

  @override
  void log(String name, [Map<String, Object> params = const {}]) {
    assert(_validName.hasMatch(name), 'bad analytics event name: $name');
    assert(!_reserved.contains(name), 'reserved analytics event name: $name');
    if (!enabled) return;
    events.add(AnalyticsEvent(name, params));
    Log.write(Log.game, 'analytics $name $params');
  }

  Iterable<AnalyticsEvent> named(String name) => events.where((e) => e.name == name);
}
