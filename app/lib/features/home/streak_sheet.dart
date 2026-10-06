import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/app_services.dart';
import '../../core/theme/tokens.dart';
import '../../core/widgets/phosphor.dart';
import '../../core/widgets/widgets.dart';
import '../../games/urban_runner/game_state.dart';
import '../../games/urban_runner/streak.dart';

/// The daily streak gift (D32): a 7-day calendar, today's gift to claim, and why tomorrow matters.
class StreakSheet extends StatefulWidget {
  const StreakSheet({super.key});

  /// Offered at most once per app session (a new process starts fresh; tests reset it per harness).
  static bool offeredThisSession = false;

  /// Shows the sheet if today's gift is waiting. Never during the first session's tutorial (first minute = play).
  static Future<void> maybeShow(BuildContext context) async {
    final state = context.read<GameState>();
    if (!state.profile.tutorialDone || state.streakStatus.claimedToday) return;
    await showModalBottomSheet<void>(
      context: context,
      backgroundColor: Colors.transparent,
      isScrollControlled: true,
      builder: (_) => const StreakSheet(),
    );
  }

  /// From the header chip: always opens (to look at the calendar, or claim if waiting).
  static Future<void> maybeShowAlways(BuildContext context) => showModalBottomSheet<void>(
        context: context,
        backgroundColor: Colors.transparent,
        isScrollControlled: true,
        builder: (_) => const StreakSheet(),
      );

  @override
  State<StreakSheet> createState() => _StreakSheetState();
}

class _StreakSheetState extends State<StreakSheet> {
  int _claimed = 0;

  Future<void> _claim() async {
    final s = context.read<AppServices>();
    final paid = await context.read<GameState>().claimStreak();
    if (!mounted) return;
    if (paid > 0) s.sounds.reward();
    setState(() => _claimed = paid);
    await Future<void>.delayed(const Duration(milliseconds: 900));
    if (mounted) Navigator.of(context).pop();
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<GameState>();
    // After claiming, show the calendar as it was offered (today ticked).
    final st = _claimed > 0
        ? StreakStatus(claimedToday: true, day: state.profile.streak, nextReward: 0)
        : state.streakStatus;
    final today = st.dayOfWeek;
    final tomorrow = DailyStreak.rewardFor(st.day + 1);
    return Container(
      decoration: const BoxDecoration(
        color: UrColors.deep,
        borderRadius: BorderRadius.vertical(top: Radius.circular(UrRadius.hero)),
        border: Border(top: BorderSide(color: UrColors.hairline)),
      ),
      padding: EdgeInsets.fromLTRB(20, 14, 20, 20 + MediaQuery.paddingOf(context).bottom),
      child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.start, children: [
        Center(child: Container(width: 40, height: 4, decoration: BoxDecoration(color: UrColors.track, borderRadius: BorderRadius.circular(2)))),
        const SizedBox(height: 18),
        Row(children: [
          Icon(PhosphorIcons.flame(PhosphorIconsStyle.fill), color: UrColors.amber, size: 30),
          const SizedBox(width: 10),
          Flexible(child: Text(st.broken ? 'A fresh start' : 'Day ${st.day} streak', style: UrText.title, overflow: TextOverflow.ellipsis)),
        ]),
        const SizedBox(height: 6),
        Text(
          st.broken
              ? 'You missed a day, so the streak starts again. Come back every day to reach the big gift on day 7.'
              : 'Come back tomorrow for ${CountUp.format(tomorrow)} sparks. Miss a day and it starts over.',
          style: UrText.body,
        ),
        const SizedBox(height: 18),
        Row(children: [
          for (var d = 1; d <= 6; d++) ...[
            if (d > 1) const SizedBox(width: 6),
            Expanded(child: _DayTile(day: d, reward: DailyStreak.rewards[d - 1], done: d < today || (d == today && st.claimedToday), today: d == today && !st.claimedToday)),
          ],
        ]),
        const SizedBox(height: 6),
        _DayTile(day: 7, reward: DailyStreak.rewards[6], done: 7 < today || (7 == today && st.claimedToday), today: today == 7 && !st.claimedToday, big: true),
        const SizedBox(height: 18),
        if (_claimed > 0)
          Center(child: Padding(
            padding: const EdgeInsets.symmetric(vertical: 14),
            child: Text('+${CountUp.format(_claimed)} sparks', style: UrText.title.copyWith(color: UrColors.success)),
          ))
        else if (!st.claimedToday)
          PressButton(key: const Key('claim_streak'), label: 'Claim ${CountUp.format(st.nextReward)} sparks', icon: PhosphorIcons.gift(PhosphorIconsStyle.fill), onPressed: _claim)
        else
          PressButton(label: 'See you tomorrow', onPressed: () => Navigator.of(context).pop()),
      ]),
    );
  }
}

class _DayTile extends StatelessWidget {
  const _DayTile({required this.day, required this.reward, required this.done, required this.today, this.big = false});
  final int day, reward;
  final bool done, today, big;

  @override
  Widget build(BuildContext context) {
    final bg = today ? UrColors.magenta : big ? UrColors.amber.withValues(alpha: done ? .25 : .9) : UrColors.surface;
    final ink = today ? UrColors.magentaInk : big && !done ? UrColors.amberInk : UrColors.text;
    final label = Column(mainAxisSize: MainAxisSize.min, children: [
      Text(big ? 'Day 7 · big gift' : 'Day $day', style: UrText.small.copyWith(color: ink.withValues(alpha: .8), fontSize: big ? 13 : 11)),
      const SizedBox(height: 4),
      if (done)
        Icon(PhosphorIcons.check(PhosphorIconsStyle.bold), color: UrColors.success, size: big ? 26 : 20)
      else
        Text(CountUp.format(reward), style: UrText.number.copyWith(color: ink, fontSize: big ? 24 : 15)),
    ]);
    return Semantics(
      label: 'Day $day, $reward sparks, ${done ? 'claimed' : today ? 'today' : 'coming up'}',
      excludeSemantics: true,
      child: AnimatedContainer(
        duration: UrMotion.normal,
        height: big ? 76 : 66,
        decoration: BoxDecoration(
          color: bg,
          borderRadius: BorderRadius.circular(14),
          border: Border.all(color: today ? UrColors.text : UrColors.hairline, width: today ? 2 : 1),
        ),
        child: big
            ? Row(mainAxisAlignment: MainAxisAlignment.center, children: [
                Icon(PhosphorIcons.gift(PhosphorIconsStyle.fill), color: ink, size: 30),
                const SizedBox(width: 12),
                Flexible(child: FittedBox(fit: BoxFit.scaleDown, child: label)),
              ])
            : Padding(padding: const EdgeInsets.all(4), child: FittedBox(fit: BoxFit.scaleDown, child: label)),
      ),
    );
  }
}
