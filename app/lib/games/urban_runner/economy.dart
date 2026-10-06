import 'dart:math' as math;

/// Economy numbers (HANDOVER §3.7, §6 rules 3 and 8). Flutter is the only owner of the economy.
abstract final class Economy {
  /// One XP for every 10 metres run.
  static const metresPerXp = 10;

  /// A run can't produce more sparks than this per metre, even with Double Sparks the whole time.
  /// Mirrors Unity's TrackRules.MaxSparksPerMetre (EditMode-tested on both sides).
  static const maxSparksPerMetre = 1.2;

  /// Total XP needed to reach [level] (level 1 = 0 XP). Gently rising curve.
  static int xpForLevel(int level) => level <= 1 ? 0 : (100 * math.pow(level - 1, 1.5)).round();

  static int levelForXp(int xp) {
    var level = 1;
    while (xpForLevel(level + 1) <= xp) {
      level++;
    }
    return level;
  }

  /// Sparks granted when reaching [level].
  static int levelUpReward(int level) => 100 * level;

  static int xpForRun(double distanceM) => (distanceM / metresPerXp).floor();

  /// Caps reported sparks to what the distance makes possible (rule 3). Returns (sparks, rejected).
  static (int, bool) sanitiseSparks(int reported, double distanceM) {
    if (reported < 0) return (0, true);
    final max = (distanceM * maxSparksPerMetre).ceil() + 5;
    return reported > max ? (max, true) : (reported, false);
  }

  /// Remote Config multipliers, applied only here, at reward time (rule 8).
  static int applyMultipliers(int sparks, {required double sparksMultiplier, required bool eventActive, required double eventMultiplier}) {
    final m = sparksMultiplier * (eventActive ? eventMultiplier : 1.0);
    return (sparks * m).round();
  }
}
