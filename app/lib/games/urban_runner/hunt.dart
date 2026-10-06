import 'streak.dart';

/// The Lantern Hunt (D32, retention): five festival lanterns to find each day, one per run. Finding all five pays a
/// prize that grows with every consecutive day the hunt is completed; a day without completing it resets that
/// streak. Pure rules on UTC day keys; progress lives in the profile.
abstract final class LanternHunt {
  static const colours = ['amber', 'pink', 'blue', 'mint', 'violet'];
  static const size = 5;

  /// Prize for completing the hunt on hunt-streak day N (cycles after a week, like the daily gift).
  static const prizes = [500, 650, 800, 1000, 1200, 1500, 2500];
  static int prizeFor(int streakDay) => prizes[(streakDay - 1) % prizes.length];

  /// Lanterns found today (progress from another day counts as 0).
  static int foundToday({required String today, required String huntDate, required int found}) => huntDate == today ? found : 0;

  /// The colour to place in the next run, or null when today's hunt is done.
  static int? nextColour({required String today, required String huntDate, required int found}) {
    final n = foundToday(today: today, huntDate: huntDate, found: found);
    return n >= size ? null : n;
  }

  /// The hunt-streak day that completing today would reach.
  static int streakIfCompletedToday({required String today, required String lastComplete, required int streak}) =>
      lastComplete == DailyStreak.yesterdayOf(today) ? streak + 1 : 1;
}
