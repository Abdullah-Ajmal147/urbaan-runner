import 'package:flutter/material.dart';
import 'package:urban_runner/core/widgets/phosphor.dart';
import 'package:provider/provider.dart';

import '../../core/theme/tokens.dart';
import '../../core/widgets/widgets.dart';
import '../../games/urban_runner/game_state.dart';
import '../../games/urban_runner/items.dart';

class ProfileScreen extends StatelessWidget {
  const ProfileScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final state = context.watch<GameState>();
    final p = state.profile;
    final courier = Items.byId(p.selectedCharacter);
    final levelP = state.xpForNextLevel == 0 ? 0.0 : state.xpIntoLevel / state.xpForNextLevel;
    return Scaffold(
      appBar: AppBar(backgroundColor: UrColors.night, title: const Text('Profile', style: UrText.cardTitle)),
      body: ListView(padding: const EdgeInsets.all(16), children: [
        ClipRRect(
          borderRadius: BorderRadius.circular(UrRadius.hero),
          child: Container(
            height: 200,
            color: Color(courier.accent),
            child: Stack(children: [
              Positioned(right: -30, top: 6, width: 190, child: Image.asset(courier.image!, semanticLabel: courier.name)),
              Positioned(
                left: 20,
                bottom: 20,
                right: 160,
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Text('Courier level', style: UrText.label.copyWith(color: Color(courier.ink))),
                  Text('${state.level}', style: UrText.hero.copyWith(fontSize: 64, color: Color(courier.ink))),
                  const SizedBox(height: 6),
                  ProgressBar(value: levelP, color: Color(courier.ink), height: 8),
                  const SizedBox(height: 4),
                  Text('${state.xpIntoLevel} / ${state.xpForNextLevel} XP', style: UrText.small.copyWith(color: Color(courier.ink))),
                ]),
              ),
            ]),
          ),
        ),
        const SizedBox(height: 14),
        GridView.count(
          crossAxisCount: 2,
          shrinkWrap: true,
          physics: const NeverScrollableScrollPhysics(),
          mainAxisSpacing: 10,
          crossAxisSpacing: 10,
          childAspectRatio: 1.45,
          children: [
            _Stat(icon: PhosphorIcons.trophy(PhosphorIconsStyle.fill), color: UrColors.success, label: 'Best distance', value: '${CountUp.format(p.bestDistance)} m'),
            _Stat(icon: PhosphorIcons.flagCheckered(PhosphorIconsStyle.fill), color: UrColors.magenta, label: 'Total runs', value: CountUp.format(p.totalRuns)),
            _Stat(icon: PhosphorIcons.sparkle(PhosphorIconsStyle.fill), color: UrColors.cyan, label: 'Sparks earned', value: CountUp.format(p.totalSparksEarned)),
            _Stat(icon: PhosphorIcons.calendarDots(PhosphorIconsStyle.fill), color: UrColors.amber, label: "Today's Daily Run", value: state.todayDailyBest > 0 ? '${CountUp.format(state.todayDailyBest)} m' : 'Not yet'),
          ],
        ),
      ]),
    );
  }
}

class _Stat extends StatelessWidget {
  const _Stat({required this.icon, required this.color, required this.label, required this.value});
  final IconData icon;
  final Color color;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) => Semantics(
        label: '$label: $value',
        excludeSemantics: true,
        child: Panel(
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
            Icon(icon, color: color, size: 22),
            Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              FittedBox(fit: BoxFit.scaleDown, alignment: Alignment.centerLeft, child: Text(value, style: UrText.number.copyWith(fontSize: 24))),
              Text(label, style: UrText.small),
            ]),
          ]),
        ),
      );
}
