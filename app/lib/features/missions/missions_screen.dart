import 'package:flutter/material.dart';
import 'package:urban_runner/core/widgets/phosphor.dart';
import 'package:provider/provider.dart';

import '../../core/app_services.dart';
import '../../core/theme/tokens.dart';
import '../../core/widgets/widgets.dart';
import '../../games/urban_runner/game_state.dart';
import '../../games/urban_runner/missions.dart';

class MissionsScreen extends StatelessWidget {
  const MissionsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final state = context.watch<GameState>();
    final missions = state.profile.missions;
    return SafeArea(
      bottom: false,
      child: ListView(padding: const EdgeInsets.fromLTRB(16, 12, 16, 24), children: [
        Row(children: [
          const Text('Missions', style: UrText.title),
          const Spacer(),
          SparksAmount(value: state.profile.sparks),
        ]),
        const SizedBox(height: 6),
        const Text('Finish one and a new one rolls in. Rewards include sparks and XP.', style: UrText.body),
        const SizedBox(height: 16),
        for (var i = 0; i < missions.length; i++) ...[
          _MissionCard(index: i, mission: missions[i]),
          const SizedBox(height: 10),
        ],
      ]),
    );
  }
}

class _MissionCard extends StatelessWidget {
  const _MissionCard({required this.index, required this.mission});
  final int index;
  final Mission mission;

  IconData get _icon => switch (mission.template.metric) {
        MissionMetric.sparks => PhosphorIcons.sparkle(PhosphorIconsStyle.fill),
        MissionMetric.distance => PhosphorIcons.personSimpleRun(PhosphorIconsStyle.fill),
        MissionMetric.jumps => PhosphorIcons.arrowFatUp(PhosphorIconsStyle.fill),
        MissionMetric.slides => PhosphorIcons.arrowFatDown(PhosphorIconsStyle.fill),
        MissionMetric.laneChanges => PhosphorIcons.arrowsLeftRight(PhosphorIconsStyle.bold),
        MissionMetric.powerUps || MissionMetric.doubleSparks => PhosphorIcons.lightning(PhosphorIconsStyle.fill),
        MissionMetric.magnet => PhosphorIcons.magnet(PhosphorIconsStyle.fill),
        MissionMetric.shield => PhosphorIcons.shieldCheck(PhosphorIconsStyle.fill),
        MissionMetric.runs => PhosphorIcons.flagCheckered(PhosphorIconsStyle.fill),
        MissionMetric.dailyRuns => PhosphorIcons.calendarDots(PhosphorIconsStyle.fill),
        MissionMetric.revives => PhosphorIcons.heartbeat(PhosphorIconsStyle.fill),
      };

  @override
  Widget build(BuildContext context) {
    final t = mission.template;
    final done = mission.claimable;
    final color = done ? UrColors.success : UrColors.cyan;
    return Panel(
      padding: const EdgeInsets.all(16),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Container(
            width: 40,
            height: 40,
            decoration: BoxDecoration(color: color.withValues(alpha: .15), borderRadius: BorderRadius.circular(12)),
            child: Icon(_icon, color: color, size: 20),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(t.title, style: UrText.cardTitle),
              const SizedBox(height: 2),
              Text('+${t.rewardSparks} sparks · +${t.rewardXp} XP${t.scope == MissionScope.run ? ' · in one run' : ''}', style: UrText.small),
            ]),
          ),
        ]),
        const SizedBox(height: 14),
        Row(children: [
          Expanded(child: ProgressBar(value: mission.fraction, color: color, height: 8)),
          const SizedBox(width: 12),
          Text('${CountUp.format(mission.progress)}/${CountUp.format(t.target)}', style: UrText.label.copyWith(color: color)),
        ]),
        if (done) ...[
          const SizedBox(height: 14),
          PressButton(
            key: Key('claim_$index'),
            label: 'Claim ${t.rewardSparks} sparks',
            icon: PhosphorIcons.gift(PhosphorIconsStyle.fill),
            color: UrColors.success,
            shade: const Color(0xFF1FB574),
            ink: UrColors.successInk,
            height: 52,
            onPressed: () async {
              final s = context.read<AppServices>();
              final got = await context.read<GameState>().claimMission(index);
              s.sounds.reward();
              if (context.mounted && got > 0) {
                ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('+$got sparks. A new mission is ready.')));
              }
            },
          ),
        ],
      ]),
    );
  }
}
