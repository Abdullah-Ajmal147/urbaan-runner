import 'dart:math' as math;

import 'items.dart';
import 'missions.dart';

class PlayerSettings {
  const PlayerSettings({this.music = true, this.sfx = true, this.haptics = true, this.reduceMotion = false, this.shareUsageData = true});

  final bool music, sfx, haptics, reduceMotion, shareUsageData;

  PlayerSettings copyWith({bool? music, bool? sfx, bool? haptics, bool? reduceMotion, bool? shareUsageData}) => PlayerSettings(
        music: music ?? this.music,
        sfx: sfx ?? this.sfx,
        haptics: haptics ?? this.haptics,
        reduceMotion: reduceMotion ?? this.reduceMotion,
        shareUsageData: shareUsageData ?? this.shareUsageData,
      );

  Map<String, Object?> toJson() => {'music': music, 'sfx': sfx, 'haptics': haptics, 'reduceMotion': reduceMotion, 'shareUsageData': shareUsageData};

  /// The subset Unity needs (session.start.settings / settings.update).
  Map<String, Object?> toUnity() => {'music': music, 'sfx': sfx, 'haptics': haptics, 'reduceMotion': reduceMotion};

  factory PlayerSettings.fromJson(Map<String, Object?>? j) => PlayerSettings(
        music: j?['music'] as bool? ?? true,
        sfx: j?['sfx'] as bool? ?? true,
        haptics: j?['haptics'] as bool? ?? true,
        reduceMotion: j?['reduceMotion'] as bool? ?? false,
        shareUsageData: j?['shareUsageData'] as bool? ?? true,
      );
}

/// Everything about the player, stored locally first (HANDOVER §7.1) and synced to Firestore when online.
class PlayerProfile {
  PlayerProfile({
    required this.createdAt,
    required this.updatedAt,
    this.sparks = 0,
    this.xp = 0,
    this.selectedCharacter = 'mira',
    this.selectedTrail = 'trail_neon',
    Set<String>? ownedItems,
    Map<String, int>? upgrades,
    this.settings = const PlayerSettings(),
    this.bestDistance = 0,
    this.totalRuns = 0,
    this.totalSparksEarned = 0,
    this.dailyBestDate = '',
    this.dailyBestDistance = 0,
    List<Mission>? missions,
    this.missionCounter = 0,
    this.onboardingDone = false,
    this.tutorialDone = false,
    this.removeAds = false,
    Set<String>? processedSessions,
    Set<String>? doubledSessions,
    Set<String>? processedPurchases,
    this.runsSinceInterstitial = 0,
    this.lastInterstitialAtMs = 0,
    this.appSessions = 0,
    this.streak = 0,
    this.streakBest = 0,
    this.lastStreakClaim = '',
    this.huntDate = '',
    this.huntFound = 0,
    this.huntStreak = 0,
    this.huntLastComplete = '',
  })  : ownedItems = ownedItems ?? {...Items.defaultOwned},
        upgrades = upgrades ?? {'magnet': 0, 'shield': 0, 'double': 0},
        missions = missions ?? Missions.starting(),
        processedSessions = processedSessions ?? <String>{},
        doubledSessions = doubledSessions ?? <String>{},
        processedPurchases = processedPurchases ?? <String>{};

  static const schemaVersion = 1;

  final DateTime createdAt;
  DateTime updatedAt;
  int sparks;
  int xp;
  String selectedCharacter;
  String selectedTrail;
  final Set<String> ownedItems;
  final Map<String, int> upgrades;
  PlayerSettings settings;
  int bestDistance;
  int totalRuns;
  int totalSparksEarned;
  String dailyBestDate;
  int dailyBestDistance;
  final List<Mission> missions;
  int missionCounter;
  bool onboardingDone;
  bool tutorialDone;
  bool removeAds;
  /// sessionIds whose rewards were already applied (bridge rule 2). Bounded.
  final Set<String> processedSessions;
  /// sessionIds whose "double sparks" ad reward was already granted.
  final Set<String> doubledSessions;
  /// Purchase tokens already granted (HANDOVER §7.5).
  final Set<String> processedPurchases;
  int runsSinceInterstitial;
  int lastInterstitialAtMs;
  int appSessions;
  /// Daily login streak (D32): consecutive days claimed, the best ever, and the UTC day of the last claim.
  int streak;
  int streakBest;
  String lastStreakClaim;
  /// Lantern Hunt (D32): the UTC day of the current progress, lanterns found that day, the completed-hunt streak
  /// and the last day it was completed.
  String huntDate;
  int huntFound;
  int huntStreak;
  String huntLastComplete;

  factory PlayerProfile.fresh(DateTime now) => PlayerProfile(createdAt: now, updatedAt: now);

  int dailyBestFor(String date) => dailyBestDate == date ? dailyBestDistance : 0;

  static Set<String> _bounded(Set<String> s, int max) => s.length <= max ? s : s.skip(s.length - max).toSet();

  Map<String, Object?> toJson() => {
        'schemaVersion': schemaVersion,
        'createdAt': createdAt.toIso8601String(),
        'updatedAt': updatedAt.toIso8601String(),
        'sparks': sparks,
        'xp': xp,
        'selectedCharacter': selectedCharacter,
        'selectedTrail': selectedTrail,
        'ownedItems': ownedItems.toList(),
        'upgrades': upgrades,
        'settings': settings.toJson(),
        'bestDistance': bestDistance,
        'totalRuns': totalRuns,
        'totalSparksEarned': totalSparksEarned,
        'dailyBest': {'date': dailyBestDate, 'distance': dailyBestDistance},
        'missions': missions.map((m) => m.toJson()).toList(),
        'missionCounter': missionCounter,
        'onboardingDone': onboardingDone,
        'tutorialDone': tutorialDone,
        'removeAds': removeAds,
        'processedSessions': _bounded(processedSessions, 300).toList(),
        'doubledSessions': _bounded(doubledSessions, 300).toList(),
        'processedPurchases': processedPurchases.toList(),
        'runsSinceInterstitial': runsSinceInterstitial,
        'lastInterstitialAtMs': lastInterstitialAtMs,
        'appSessions': appSessions,
        'streak': streak,
        'streakBest': streakBest,
        'lastStreakClaim': lastStreakClaim,
        'huntDate': huntDate,
        'huntFound': huntFound,
        'huntStreak': huntStreak,
        'huntLastComplete': huntLastComplete,
      };

  factory PlayerProfile.fromJson(Map<String, Object?> j) {
    int i(String k) => (j[k] as num?)?.toInt() ?? 0;
    Set<String> set(String k) => {...((j[k] as List?) ?? const []).cast<String>()};
    final daily = j['dailyBest'] as Map<String, Object?>?;
    final ups = (j['upgrades'] as Map?)?.map((k, v) => MapEntry(k as String, (v as num).toInt()));
    final missions = ((j['missions'] as List?) ?? const []).map((m) => Mission.fromJson((m as Map).cast<String, Object?>())).toList();
    return PlayerProfile(
      createdAt: DateTime.tryParse(j['createdAt'] as String? ?? '') ?? DateTime.now(),
      updatedAt: DateTime.tryParse(j['updatedAt'] as String? ?? '') ?? DateTime.now(),
      sparks: math.max(0, i('sparks')),
      xp: math.max(0, i('xp')),
      selectedCharacter: j['selectedCharacter'] as String? ?? 'mira',
      selectedTrail: j['selectedTrail'] as String? ?? 'trail_neon',
      ownedItems: {...Items.defaultOwned, ...set('ownedItems')},
      upgrades: {'magnet': 0, 'shield': 0, 'double': 0, ...?ups},
      settings: PlayerSettings.fromJson((j['settings'] as Map?)?.cast<String, Object?>()),
      bestDistance: i('bestDistance'),
      totalRuns: i('totalRuns'),
      totalSparksEarned: i('totalSparksEarned'),
      dailyBestDate: daily?['date'] as String? ?? '',
      dailyBestDistance: (daily?['distance'] as num?)?.toInt() ?? 0,
      missions: missions.length == Missions.activeCount ? missions : null,
      missionCounter: i('missionCounter'),
      onboardingDone: j['onboardingDone'] as bool? ?? false,
      tutorialDone: j['tutorialDone'] as bool? ?? false,
      removeAds: j['removeAds'] as bool? ?? false,
      processedSessions: set('processedSessions'),
      doubledSessions: set('doubledSessions'),
      processedPurchases: set('processedPurchases'),
      runsSinceInterstitial: i('runsSinceInterstitial'),
      lastInterstitialAtMs: i('lastInterstitialAtMs'),
      appSessions: i('appSessions'),
      streak: i('streak'),
      streakBest: i('streakBest'),
      lastStreakClaim: j['lastStreakClaim'] as String? ?? '',
      huntDate: j['huntDate'] as String? ?? '',
      huntFound: i('huntFound').clamp(0, 5),
      huntStreak: i('huntStreak'),
      huntLastComplete: j['huntLastComplete'] as String? ?? '',
    );
  }

  /// Merge with a remote copy (HANDOVER §7.1): max for fields that only grow, union for owned items,
  /// latest write wins for selections and settings. Balances can't be merged by max (they go down when
  /// spent), so the newer document's balance wins.
  PlayerProfile mergedWith(PlayerProfile remote) {
    final remoteNewer = remote.updatedAt.isAfter(updatedAt);
    final newer = remoteNewer ? remote : this;
    final dailyDate = dailyBestDate.compareTo(remote.dailyBestDate) >= 0 ? dailyBestDate : remote.dailyBestDate;
    return PlayerProfile(
      createdAt: createdAt.isBefore(remote.createdAt) ? createdAt : remote.createdAt,
      updatedAt: newer.updatedAt,
      sparks: newer.sparks,
      xp: math.max(xp, remote.xp),
      selectedCharacter: newer.selectedCharacter,
      selectedTrail: newer.selectedTrail,
      ownedItems: {...ownedItems, ...remote.ownedItems},
      upgrades: {for (final k in upgrades.keys) k: math.max(upgrades[k] ?? 0, remote.upgrades[k] ?? 0)},
      settings: newer.settings,
      bestDistance: math.max(bestDistance, remote.bestDistance),
      totalRuns: math.max(totalRuns, remote.totalRuns),
      totalSparksEarned: math.max(totalSparksEarned, remote.totalSparksEarned),
      dailyBestDate: dailyDate,
      dailyBestDistance: math.max(dailyBestFor(dailyDate), remote.dailyBestFor(dailyDate)),
      missions: newer.missions,
      missionCounter: math.max(missionCounter, remote.missionCounter),
      onboardingDone: onboardingDone || remote.onboardingDone,
      tutorialDone: tutorialDone || remote.tutorialDone,
      removeAds: removeAds || remote.removeAds,
      processedSessions: {...processedSessions, ...remote.processedSessions},
      doubledSessions: {...doubledSessions, ...remote.doubledSessions},
      processedPurchases: {...processedPurchases, ...remote.processedPurchases},
      runsSinceInterstitial: runsSinceInterstitial,
      lastInterstitialAtMs: lastInterstitialAtMs,
      appSessions: math.max(appSessions, remote.appSessions),
      // The newer claim wins as a whole (streak and its date belong together).
      streak: lastStreakClaim.compareTo(remote.lastStreakClaim) >= 0 ? streak : remote.streak,
      lastStreakClaim: lastStreakClaim.compareTo(remote.lastStreakClaim) >= 0 ? lastStreakClaim : remote.lastStreakClaim,
      streakBest: math.max(streakBest, remote.streakBest),
      // Hunt progress: the later day wins, and on the same day the most lanterns found.
      huntDate: huntDate.compareTo(remote.huntDate) >= 0 ? huntDate : remote.huntDate,
      huntFound: huntDate == remote.huntDate ? math.max(huntFound, remote.huntFound) : (huntDate.compareTo(remote.huntDate) > 0 ? huntFound : remote.huntFound),
      huntStreak: huntLastComplete.compareTo(remote.huntLastComplete) >= 0 ? huntStreak : remote.huntStreak,
      huntLastComplete: huntLastComplete.compareTo(remote.huntLastComplete) >= 0 ? huntLastComplete : remote.huntLastComplete,
    );
  }
}

/// Daily Run: one fixed seed per UTC day, the same for every player (HANDOVER §3.7).
abstract final class DailyRun {
  static String dateKey(DateTime nowUtc) {
    final d = nowUtc.toUtc();
    return '${d.year.toString().padLeft(4, '0')}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';
  }

  /// FNV-1a of the date key: stable on every device and platform.
  static int seedFor(String dateKey) {
    var h = 0x811C9DC5;
    for (final c in dateKey.codeUnits) {
      h ^= c;
      h = (h * 0x01000193) & 0xFFFFFFFF;
    }
    return h;
  }

  static Duration timeLeft(DateTime nowUtc) {
    final d = nowUtc.toUtc();
    final tomorrow = DateTime.utc(d.year, d.month, d.day + 1);
    return tomorrow.difference(d);
  }
}
