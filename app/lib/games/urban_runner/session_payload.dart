import '../../core/remote_config/remote_config.dart';
import 'hunt.dart';
import 'profile.dart';

/// Builds session.start's payload (bridge contract v3) from the profile and Remote Config.
abstract final class SessionPayload {
  static Map<String, Object?> build({
    required PlayerProfile profile,
    required RemoteConfig config,
    required String mode,
    required int seed,
    Map<String, Object?>? testMode,
    String? today,
  }) {
    // v3 Lantern Hunt: the colour still to find today (never in the tutorial).
    final huntColour = today == null || mode == 'tutorial'
        ? null
        : LanternHunt.nextColour(today: today, huntDate: profile.huntDate, found: profile.huntFound);
    return {
        'seed': seed,
        'mode': mode,
        'character': profile.selectedCharacter,
        'trail': profile.selectedTrail,
        'upgrades': {
          'magnet': profile.upgrades['magnet'] ?? 0,
          'shield': profile.upgrades['shield'] ?? 0,
          'double': profile.upgrades['double'] ?? 0,
        },
        'config': {
          'start_speed': config.number('start_speed'),
          'max_speed': config.number('max_speed'),
          'speed_ramp_per_min': config.number('speed_ramp_per_min'),
          'revives_per_run': config.integer('revives_per_run'),
          'revive_enabled': config.flag('revive_enabled'),
        },
        'settings': profile.settings.toUnity(),
        'bestDistance': profile.bestDistance,
        'testMode': ?testMode,
        if (huntColour != null)
          'hunt': {'colour': huntColour, 'found': LanternHunt.foundToday(today: today!, huntDate: profile.huntDate, found: profile.huntFound)},
      };
  }
}
