import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/theme/tokens.dart';
import '../../core/widgets/phosphor.dart';
import '../../core/widgets/widgets.dart';
import '../../games/urban_runner/game_state.dart';
import '../../games/urban_runner/hunt.dart';

/// The five Lantern Hunt colours, as shown in the app (same order as Unity's lanterns).
const huntColours = [Color(0xFFFFB547), Color(0xFFFF3DA6), Color(0xFF2EE6FF), Color(0xFF3DFFA2), Color(0xFFB07CFF)];

/// Five little lanterns: lit when found today.
class HuntLanterns extends StatelessWidget {
  const HuntLanterns({super.key, required this.found, this.size = 22, this.justFound = -1});
  final int found;
  final double size;
  final int justFound;

  @override
  Widget build(BuildContext context) => Row(mainAxisSize: MainAxisSize.min, children: [
        for (var i = 0; i < LanternHunt.size; i++)
          Padding(
            padding: EdgeInsets.only(right: size * 0.3),
            child: AnimatedScale(
              scale: i == justFound ? 1.25 : 1,
              duration: UrMotion.slow,
              child: Container(
                width: size * 0.8,
                height: size,
                decoration: BoxDecoration(
                  color: i < found ? huntColours[i] : UrColors.track,
                  borderRadius: BorderRadius.circular(size * 0.4),
                  boxShadow: i < found ? [BoxShadow(color: huntColours[i].withValues(alpha: .55), blurRadius: size * 0.6)] : null,
                ),
              ),
            ),
          ),
      ]);
}

/// Home card: today's Lantern Hunt (D32 retention).
class HuntCard extends StatelessWidget {
  const HuntCard({super.key});

  @override
  Widget build(BuildContext context) {
    final state = context.watch<GameState>();
    final found = state.huntFoundToday;
    final done = found >= LanternHunt.size;
    final streak = state.huntStreakNow;
    final nextPrize = LanternHunt.prizeFor(
        LanternHunt.streakIfCompletedToday(today: state.todayKey, lastComplete: state.profile.huntLastComplete, streak: state.profile.huntStreak));
    return Panel(
      semanticsLabel: done
          ? 'Lantern Hunt complete today. Hunt streak $streak days.'
          : 'Lantern Hunt: $found of 5 lanterns found today. Find all five for $nextPrize sparks.',
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Row(children: [
          Text('Lantern Hunt', style: UrText.cardTitle.copyWith(color: UrColors.amber)),
          const Spacer(),
          if (streak > 0) ...[
            Icon(PhosphorIcons.flame(PhosphorIconsStyle.fill), size: 16, color: UrColors.amber),
            const SizedBox(width: 4),
            Text('$streak', style: UrText.label.copyWith(color: UrColors.amber, fontWeight: FontWeight.w800)),
          ],
        ]),
        const SizedBox(height: 10),
        HuntLanterns(found: found),
        const SizedBox(height: 8),
        Text(
          done
              ? 'All five found. New lanterns tomorrow, keep the streak going.'
              : 'One lantern glows somewhere in every run. Find all five today for ${CountUp.format(nextPrize)} sparks.',
          style: UrText.small,
        ),
      ]),
    );
  }
}
