import 'dart:math' as math;

import 'package:flutter/foundation.dart';

import '../../core/analytics/analytics.dart';
import '../../core/iap/store.dart';
import '../../core/log.dart';
import '../../core/remote_config/remote_config.dart';
import '../../core/storage/local_store.dart';
import 'economy.dart';
import 'hunt.dart';
import 'items.dart';
import 'missions.dart';
import 'profile.dart';
import 'run_result.dart';
import 'streak.dart';

/// What a finished run earned (shown on Results).
class RunReward {
  const RunReward({
    required this.result,
    required this.sparksCredited,
    required this.xpGained,
    required this.levelBefore,
    required this.levelAfter,
    required this.levelUpSparks,
    required this.previousBest,
    required this.newBest,
    required this.dailyBest,
    required this.missionsCompleted,
    required this.unlocked,
    required this.rejected,
    required this.alreadyProcessed,
    this.huntFoundNow = false,
    this.huntFoundToday = 0,
    this.huntPrize = 0,
    this.huntStreak = 0,
  });

  /// Lantern Hunt (v3): a lantern was found this run; how many today; the prize if this completed the hunt.
  final bool huntFoundNow;
  final int huntFoundToday;
  final int huntPrize;
  final int huntStreak;

  final RunResult result;
  final int sparksCredited;
  final int xpGained;
  final int levelBefore;
  final int levelAfter;
  final int levelUpSparks;
  final int previousBest;
  final bool newBest;
  final bool dailyBest;
  final List<String> missionsCompleted;
  final List<String> unlocked;
  final bool rejected;
  final bool alreadyProcessed;
}

abstract final class Products {
  static const removeAds = 'remove_ads';
  static const starterPack = 'starter_pack';
  static const starterSparks = 2000;
}

/// The player's progress and the economy (Flutter owns it; HANDOVER §5.2). Local-first, saved on every change.
class GameState extends ChangeNotifier {
  GameState({required this.store, required this.analytics, required this.config, DateTime Function()? clock})
      : _clock = clock ?? DateTime.now;

  static const profileKey = 'urban_runner.profile.v1';

  final LocalStore store;
  final Analytics analytics;
  final RemoteConfig config;
  final DateTime Function() _clock;

  late PlayerProfile _p;
  PlayerProfile get profile => _p;

  /// Called after every save so a sync service can push the profile (M4).
  void Function(PlayerProfile)? onSaved;

  /// The clock everything date-based uses (Daily Run, countdowns); a fake in tests.
  DateTime now() => _clock();

  int get level => Economy.levelForXp(_p.xp);
  int get xpIntoLevel => _p.xp - Economy.xpForLevel(level);
  int get xpForNextLevel => Economy.xpForLevel(level + 1) - Economy.xpForLevel(level);
  String get todayKey => DailyRun.dateKey(_clock());
  int get dailySeed => DailyRun.seedFor(todayKey);
  int get todayDailyBest => _p.dailyBestFor(todayKey);
  bool isOwned(String itemId) => _p.ownedItems.contains(itemId);

  Future<void> load() async {
    final doc = await store.read(profileKey);
    _p = doc == null ? PlayerProfile.fresh(_clock()) : PlayerProfile.fromJson(doc);
    _p.appSessions++;
    analytics.enabled = _p.settings.shareUsageData;
    await _save(notify: false);
  }

  /// Replace the profile (e.g. merged with the cloud copy) and persist it.
  Future<void> replaceProfile(PlayerProfile p) async {
    _p = p;
    await _save();
  }

  Future<void> _save({bool notify = true}) async {
    _p.updatedAt = _clock();
    await store.write(profileKey, _p.toJson());
    onSaved?.call(_p);
    if (notify) notifyListeners();
  }

  // ---------------- runs ----------------

  /// Applies a run's rewards exactly once per sessionId (bridge rule 2). A duplicate returns the same
  /// shape with [RunReward.alreadyProcessed] and changes nothing.
  Future<RunReward> applyRun(RunResult raw) async {
    final r = raw.sanitised();
    final levelBefore = level;
    if (_p.processedSessions.contains(r.sessionId)) {
      Log.write(Log.bridge, 'duplicate session.ended ${r.sessionId} ignored');
      return RunReward(result: r, sparksCredited: 0, xpGained: 0, levelBefore: levelBefore, levelAfter: levelBefore, levelUpSparks: 0,
          previousBest: _p.bestDistance, newBest: false, dailyBest: false, missionsCompleted: const [], unlocked: const [], rejected: false, alreadyProcessed: true);
    }
    _p.processedSessions.add(r.sessionId);

    final (sparks, rejected) = Economy.sanitiseSparks(r.sparks, r.distanceM);
    if (rejected) analytics.log('result_rejected', {'reason': 'sparks_exceed_distance'});
    final credited = Economy.applyMultipliers(sparks,
        sparksMultiplier: config.number('sparks_multiplier'),
        eventActive: config.flag('event_active'),
        eventMultiplier: config.number('event_sparks_multiplier'));

    final distance = r.distanceM.floor();
    final previousBest = _p.bestDistance;
    final newBest = distance > previousBest && r.mode != 'tutorial';
    if (newBest) _p.bestDistance = distance;
    _p.totalRuns++;
    _p.runsSinceInterstitial++;
    _p.sparks += credited;
    _p.totalSparksEarned += credited;
    if (r.tutorialCompleted || r.mode == 'tutorial') _p.tutorialDone = true;

    var dailyBest = false;
    if (r.isDaily) {
      final today = todayKey;
      final prev = _p.dailyBestFor(today);
      dailyBest = distance > prev;
      if (dailyBest) {
        _p.dailyBestDate = today;
        _p.dailyBestDistance = distance;
      }
      analytics.log('daily_run_complete', {'distance_m': distance, 'is_best': dailyBest ? 1 : 0});
    }

    final (huntFoundNow, huntPrize) = _applyHunt(r);

    final missionsDone = Missions.apply(_p.missions, r);
    final xp = Economy.xpForRun(r.distanceM);
    final (levelUpSparks, unlocked) = _gainXp(xp);

    analytics.log('game_end', {
      'session_id': r.sessionId,
      'mode': r.mode,
      'distance_m': distance,
      'sparks': credited,
      'duration_s': r.durationS.round(),
      'cause': r.cause,
      'revives': r.revivesUsed,
    });
    await _save();
    return RunReward(
      result: r,
      sparksCredited: credited,
      xpGained: xp,
      levelBefore: levelBefore,
      levelAfter: level,
      levelUpSparks: levelUpSparks,
      previousBest: previousBest,
      newBest: newBest,
      dailyBest: dailyBest,
      missionsCompleted: missionsDone,
      unlocked: unlocked,
      rejected: rejected,
      alreadyProcessed: false,
      huntFoundNow: huntFoundNow,
      huntFoundToday: huntFoundToday,
      huntPrize: huntPrize,
      huntStreak: _p.huntStreak,
    );
  }

  // ---------------- Lantern Hunt (D32) ----------------

  int get huntFoundToday => LanternHunt.foundToday(today: todayKey, huntDate: _p.huntDate, found: _p.huntFound);

  /// The hunt streak as the player sees it today: kept if completed today or yesterday, otherwise already lost.
  int get huntStreakNow =>
      _p.huntLastComplete == todayKey || _p.huntLastComplete == DailyStreak.yesterdayOf(todayKey) ? _p.huntStreak : 0;

  /// Counts this run's lantern (once per run: applyRun is already once per sessionId). Returns (found, prize paid).
  (bool, int) _applyHunt(RunResult r) {
    if (!r.huntFound || r.mode == 'tutorial') return (false, 0);
    final today = todayKey;
    final before = LanternHunt.foundToday(today: today, huntDate: _p.huntDate, found: _p.huntFound);
    if (before >= LanternHunt.size) return (false, 0);
    _p.huntDate = today;
    _p.huntFound = before + 1;
    analytics.log('hunt_lantern_found', {'count': _p.huntFound});
    if (_p.huntFound < LanternHunt.size) return (true, 0);
    _p.huntStreak = LanternHunt.streakIfCompletedToday(today: today, lastComplete: _p.huntLastComplete, streak: _p.huntStreak);
    _p.huntLastComplete = today;
    final prize = LanternHunt.prizeFor(_p.huntStreak);
    _p.sparks += prize;
    _p.totalSparksEarned += prize;
    analytics.log('hunt_complete', {'streak': _p.huntStreak, 'reward': prize});
    return (true, prize);
  }

  /// Adds XP, pays level-up rewards and unlocks level items. Returns (sparks granted, item ids unlocked).
  (int, List<String>) _gainXp(int xp) {
    final before = level;
    _p.xp += xp;
    final after = level;
    var sparks = 0;
    final unlocked = <String>[];
    for (var l = before + 1; l <= after; l++) {
      final reward = Economy.levelUpReward(l);
      sparks += reward;
      analytics.log('level_up', {'level': l});
      for (final item in Items.all) {
        if (item.unlock == UnlockKind.level && item.level == l && _p.ownedItems.add(item.id)) {
          unlocked.add(item.id);
          analytics.log('item_unlock', {'item_id': item.id, 'source': 'level'});
        }
      }
    }
    _p.sparks += sparks;
    _p.totalSparksEarned += sparks;
    return (sparks, unlocked);
  }

  /// "Double sparks (ad)" on Results: grants the run's credited sparks again, once per session.
  Future<bool> grantDoubleReward(RunReward reward) async {
    final id = reward.result.sessionId;
    if (reward.alreadyProcessed || _p.doubledSessions.contains(id) || !config.flag('double_reward_enabled')) return false;
    _p.doubledSessions.add(id);
    _p.sparks += reward.sparksCredited;
    _p.totalSparksEarned += reward.sparksCredited;
    await _save();
    return true;
  }

  // ---------------- missions ----------------

  // ---------------- daily streak (D32) ----------------

  StreakStatus get streakStatus => DailyStreak.status(today: todayKey, lastClaim: _p.lastStreakClaim, streak: _p.streak);

  /// Claims today's streak gift: once per UTC day, whatever happens (double taps, restarts). Returns sparks paid.
  Future<int> claimStreak() async {
    final st = streakStatus;
    if (st.claimedToday) return 0;
    _p.streak = st.day;
    _p.lastStreakClaim = todayKey;
    if (_p.streak > _p.streakBest) _p.streakBest = _p.streak;
    _p.sparks += st.nextReward;
    _p.totalSparksEarned += st.nextReward;
    analytics.log('streak_claim', {'day': st.day, 'reward': st.nextReward});
    await _save();
    return st.nextReward;
  }

  Future<int> claimMission(int index) async {
    final m = _p.missions[index];
    if (!m.claimable) return 0;
    final t = m.template;
    _p.sparks += t.rewardSparks;
    _p.totalSparksEarned += t.rewardSparks;
    _gainXp(t.rewardXp);
    analytics.log('mission_complete', {'mission_id': t.id, 'reward': t.rewardSparks});
    _p.missionCounter++;
    _p.missions[index] = Mission(templateId: Missions.next(_p.missions, _p.missionCounter, exclude: t.id));
    await _save();
    return t.rewardSparks;
  }

  // ---------------- locker ----------------

  Future<bool> buyUpgrade(PowerUp p) async {
    final key = Upgrades.key(p);
    final lvl = _p.upgrades[key] ?? 0;
    final cost = Upgrades.nextCost(lvl);
    if (cost == null || _p.sparks < cost) return false;
    _p.sparks -= cost;
    _p.upgrades[key] = lvl + 1;
    analytics.log('upgrade_buy', {'powerup': key, 'level': lvl + 1});
    await _save();
    return true;
  }

  Future<bool> buyItem(LockerItem item) async {
    if (isOwned(item.id) || item.unlock != UnlockKind.sparks || _p.sparks < item.price) return false;
    _p.sparks -= item.price;
    _p.ownedItems.add(item.id);
    analytics.log('item_unlock', {'item_id': item.id, 'source': 'sparks'});
    await _save();
    return true;
  }

  Future<void> equip(LockerItem item) async {
    if (!isOwned(item.id)) return;
    if (item.kind == ItemKind.character) {
      _p.selectedCharacter = item.id;
    } else {
      _p.selectedTrail = item.id;
    }
    await _save();
  }

  // ---------------- purchases (HANDOVER §7.5) ----------------

  /// Grants a purchase at most once per token. Returns true if something was granted now.
  Future<bool> grantPurchase(PurchaseUpdate u) async {
    final token = u.token;
    if (token == null || (u.status != PurchaseStatus.purchased && u.status != PurchaseStatus.restored)) return false;
    if (_p.processedPurchases.contains(token)) return false;
    _p.processedPurchases.add(token);
    if (u.productId == Products.removeAds) {
      _p.removeAds = true;
    } else if (u.productId == Products.starterPack) {
      if (u.status == PurchaseStatus.purchased) {
        _p.sparks += Products.starterSparks;
        _p.totalSparksEarned += Products.starterSparks;
      }
      if (_p.ownedItems.add(Items.lantern.id)) analytics.log('item_unlock', {'item_id': Items.lantern.id, 'source': 'iap'});
    }
    await _save();
    return true;
  }

  // ---------------- settings / onboarding ----------------

  Future<void> updateSettings(PlayerSettings s) async {
    _p.settings = s;
    analytics.enabled = s.shareUsageData;
    await _save();
  }

  Future<void> completeOnboarding() async {
    _p.onboardingDone = true;
    await _save();
  }

  Future<void> replayTutorial() async {
    _p.tutorialDone = false;
    await _save();
  }

  Future<void> markInterstitialShown() async {
    _p.runsSinceInterstitial = 0;
    _p.lastInterstitialAtMs = _clock().millisecondsSinceEpoch;
    await _save();
  }

  /// Settings → Reset progress (with confirmation in the UI). Keeps purchases: they belong to the account.
  Future<void> resetProgress() async {
    final keep = (_p.removeAds, {..._p.processedPurchases}, _p.ownedItems.contains(Items.lantern.id), _p.settings);
    _p = PlayerProfile.fresh(_clock())
      ..removeAds = keep.$1
      ..processedPurchases.addAll(keep.$2)
      ..settings = keep.$4
      ..onboardingDone = true
      ..appSessions = math.max(1, _p.appSessions);
    if (keep.$3) _p.ownedItems.add(Items.lantern.id);
    await _save();
  }
}
