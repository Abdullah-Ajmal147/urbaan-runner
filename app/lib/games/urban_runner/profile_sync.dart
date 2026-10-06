import 'dart:math' as math;

import '../../core/sync/cloud_sync.dart';
import 'economy.dart';
import 'profile.dart';

/// Urban Runner's cloud layout (HANDOVER §7.1):
///   users/{uid}                         shared profile: balance, XP, selections, owned items, settings
///   games/urban_runner/players/{uid}    this game's progress: best, runs, daily best, missions
/// Device-only fields (processed session ids, ad pacing, Remove Ads, which Play restores) stay local.
class UrbanRunnerSyncAdapter implements SyncAdapter<PlayerProfile> {
  /// The rules allow sparks to rise by at most this much per write (firebase/firestore.rules).
  static const maxSparksRisePerWrite = 20000;

  static String userPath(String uid) => 'users/$uid';
  static String playerPath(String uid) => 'games/urban_runner/players/$uid';

  @override
  List<String> paths(String uid) => [userPath(uid), playerPath(uid)];

  @override
  PlayerProfile? decode(String uid, Map<String, Map<String, Object?>?> docs) {
    final u = docs[userPath(uid)];
    final p = docs[playerPath(uid)];
    if (u == null && p == null) return null;
    DateTime? t(Object? v) => v is DateTime ? v : null;
    final updated = [t(u?['lastActiveAt']), t(p?['updatedAt'])].whereType<DateTime>().fold<DateTime?>(null, (a, b) => a == null || b.isAfter(a) ? b : a);
    return PlayerProfile.fromJson({
      ...?u,
      ...?p,
      'createdAt': t(u?['createdAt'])?.toIso8601String(),
      // A missing half counts as older than anything on the device.
      'updatedAt': (updated ?? DateTime.fromMillisecondsSinceEpoch(0)).toIso8601String(),
    });
  }

  @override
  Map<String, Map<String, Object?>> encode(String uid, PlayerProfile v, Map<String, Map<String, Object?>?> remote) {
    final remoteSparks = (remote[userPath(uid)]?['sparks'] as num?)?.toInt() ?? 0;
    final sparks = math.min(v.sparks, remoteSparks + maxSparksRisePerWrite);
    return {
      userPath(uid): {
        'createdAt': (remote[userPath(uid)]?['createdAt'] as DateTime?) ?? v.createdAt.toUtc(),
        'lastActiveAt': v.updatedAt.toUtc(),
        'schemaVersion': PlayerProfile.schemaVersion,
        'sparks': sparks,
        'xp': v.xp,
        'level': Economy.levelForXp(v.xp),
        'selectedCharacter': v.selectedCharacter,
        'selectedTrail': v.selectedTrail,
        'ownedItems': (v.ownedItems.toList()..sort()),
        'upgrades': Map.of(v.upgrades),
        'settings': v.settings.toJson(),
      },
      playerPath(uid): {
        'bestDistance': v.bestDistance,
        'totalRuns': v.totalRuns,
        'totalSparksEarned': v.totalSparksEarned,
        if (v.dailyBestDate.isNotEmpty) 'dailyBest': {'date': v.dailyBestDate, 'distance': v.dailyBestDistance},
        'missions': v.missions.map((m) => m.toJson()).toList(),
        'missionCounter': v.missionCounter,
        'tutorialDone': v.tutorialDone,
        'updatedAt': v.updatedAt.toUtc(),
      },
    };
  }

  @override
  PlayerProfile merge(PlayerProfile local, PlayerProfile remote) => local.mergedWith(remote);
}
