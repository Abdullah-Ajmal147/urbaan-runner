import 'profile.dart';

/// The daily login streak (D32, retention): open the game on consecutive days and each day's gift grows; day 7 is
/// the big one, then the week starts again. Missing a day resets the streak to day 1 (that's what makes coming
/// back tomorrow matter). Pure rules: dates are UTC day keys from [DailyRun.dateKey].
abstract final class DailyStreak {
  /// Sparks for days 1–7 of a week.
  static const rewards = [100, 150, 200, 300, 400, 600, 1500];

  static String yesterdayOf(String dateKey) {
    final d = DateTime.parse('${dateKey}T00:00:00Z');
    return DailyRun.dateKey(d.subtract(const Duration(days: 1)));
  }

  /// What today looks like before claiming.
  static StreakStatus status({required String today, required String lastClaim, required int streak}) {
    if (lastClaim == today) return StreakStatus(claimedToday: true, day: streak, nextReward: 0);
    final continues = lastClaim.isNotEmpty && lastClaim == yesterdayOf(today);
    final day = continues ? streak + 1 : 1;
    return StreakStatus(claimedToday: false, day: day, nextReward: rewardFor(day), broken: !continues && streak > 0);
  }

  /// Day N of the streak pays the reward of its place in the week (day 8 = day 1's reward, day 14 = the big one).
  static int rewardFor(int day) => rewards[(day - 1) % rewards.length];
}

class StreakStatus {
  const StreakStatus({required this.claimedToday, required this.day, required this.nextReward, this.broken = false});

  final bool claimedToday;

  /// The streak day today is (or becomes when claimed).
  final int day;

  /// Sparks claimable now (0 when already claimed).
  final int nextReward;

  /// The player had a streak but missed a day.
  final bool broken;

  /// Position in the 7-day calendar (1–7).
  int get dayOfWeek => (day - 1) % DailyStreak.rewards.length + 1;
}
