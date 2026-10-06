import '../analytics/analytics.dart';
import '../log.dart';

/// One Remote Config key with its in-app default and allowed range (HANDOVER §7.3).
class ConfigSpec {
  const ConfigSpec.number(this.key, num this.defaultValue, this.min, this.max) : isBool = false;
  const ConfigSpec.flag(this.key, bool this.defaultValue)
      : isBool = true,
        min = null,
        max = null;

  final String key;
  final Object defaultValue;
  final num? min;
  final num? max;
  final bool isBool;
}

/// Where fetched values come from: Firebase Remote Config, or nothing (defaults only).
abstract class RemoteConfigSource {
  /// Values activated at this app start (fetched in a previous session). Never blocks on the network.
  Future<Map<String, Object?>> activatedValues();

  /// Fetch in the background for the *next* app start (never mid-run).
  Future<void> fetchForNextStart();
}

class DefaultsOnlySource implements RemoteConfigSource {
  const DefaultsOnlySource([this.values = const {}]);
  final Map<String, Object?> values;
  @override
  Future<Map<String, Object?>> activatedValues() async => values;
  @override
  Future<void> fetchForNextStart() async {}
}

/// Typed, clamped config values. Bad values never break the game: invalid → default, out of range → clamped,
/// and both are reported as `config_invalid` (key) analytics events.
class RemoteConfig {
  RemoteConfig(this.specs, {Analytics? analytics}) : _analytics = analytics { // ignore: prefer_initializing_formals
    for (final s in specs) {
      _values[s.key] = s.defaultValue;
    }
  }

  final List<ConfigSpec> specs;
  final Analytics? _analytics;
  final _values = <String, Object>{};

  Future<void> activate(RemoteConfigSource source) async {
    Map<String, Object?> raw;
    try {
      raw = await source.activatedValues();
    } catch (e) {
      Log.write(Log.error, 'remote config unavailable, using defaults: $e');
      return;
    }
    for (final s in specs) {
      if (!raw.containsKey(s.key)) continue;
      _values[s.key] = _validate(s, raw[s.key]);
    }
  }

  Object _validate(ConfigSpec s, Object? v) {
    if (s.isBool) {
      if (v is bool) return v;
      if (v is String && (v == 'true' || v == 'false')) return v == 'true';
      _invalid(s, v);
      return s.defaultValue;
    }
    final n = v is num ? v : (v is String ? num.tryParse(v) : null);
    if (n == null || n.isNaN || n.isInfinite) {
      _invalid(s, v);
      return s.defaultValue;
    }
    if (n < s.min! || n > s.max!) {
      _invalid(s, v);
      return n.clamp(s.min!, s.max!);
    }
    return n;
  }

  void _invalid(ConfigSpec s, Object? v) {
    Log.write(Log.error, 'config_invalid ${s.key}=$v');
    _analytics?.log('config_invalid', {'key': s.key});
  }

  double number(String key) => (_values[key] as num).toDouble();
  int integer(String key) => (_values[key] as num).round();
  bool flag(String key) => _values[key] as bool;
}

/// Urban Runner's keys (HANDOVER §7.3).
abstract final class UrConfig {
  static const specs = [
    ConfigSpec.number('start_speed', 8, 5, 12),
    ConfigSpec.number('max_speed', 22, 12, 30),
    ConfigSpec.number('speed_ramp_per_min', 3, 0.5, 6),
    ConfigSpec.flag('revive_enabled', true),
    ConfigSpec.number('revives_per_run', 1, 0, 2),
    ConfigSpec.flag('double_reward_enabled', true),
    ConfigSpec.number('sparks_multiplier', 1.0, 0.5, 3.0),
    ConfigSpec.number('interstitial_first_after_runs', 3, 2, 10),
    ConfigSpec.number('interstitial_every_n_runs', 3, 2, 10),
    ConfigSpec.number('interstitial_min_interval_s', 120, 60, 600),
    ConfigSpec.flag('event_active', false),
    ConfigSpec.number('event_sparks_multiplier', 2.0, 1.0, 3.0),
  ];
}
