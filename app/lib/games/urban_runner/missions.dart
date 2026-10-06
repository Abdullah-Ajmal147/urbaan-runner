import 'dart:math' as math;

import 'run_result.dart';

/// "In one run" missions reset every run; "total" missions add up across runs.
enum MissionScope { run, total }

enum MissionMetric { sparks, distance, jumps, slides, laneChanges, powerUps, magnet, shield, doubleSparks, runs, dailyRuns, revives }

class MissionTemplate {
  const MissionTemplate(this.id, this.title, this.metric, this.scope, this.target, this.rewardSparks, this.rewardXp);

  final String id;
  final String title;
  final MissionMetric metric;
  final MissionScope scope;
  final int target;
  final int rewardSparks;
  final int rewardXp;

  /// How much a run contributes to this mission's metric.
  int valueFrom(RunResult r) => switch (metric) {
        MissionMetric.sparks => r.sparks,
        MissionMetric.distance => r.distanceM.floor(),
        MissionMetric.jumps => r.stats.jumps,
        MissionMetric.slides => r.stats.slides,
        MissionMetric.laneChanges => r.stats.laneChanges,
        MissionMetric.powerUps => r.stats.powerUps,
        MissionMetric.magnet => r.stats.magnet,
        MissionMetric.shield => r.stats.shield,
        MissionMetric.doubleSparks => r.stats.doubleSparks,
        MissionMetric.runs => 1,
        MissionMetric.dailyRuns => r.isDaily ? 1 : 0,
        MissionMetric.revives => r.revivesUsed,
      };
}

/// The pool (HANDOVER §3.7: at least 20 templates). Rewards scale with effort.
abstract final class MissionPool {
  static const all = [
    MissionTemplate('sparks_run_300', 'Collect 300 sparks in one run', MissionMetric.sparks, MissionScope.run, 300, 250, 60),
    MissionTemplate('sparks_run_600', 'Collect 600 sparks in one run', MissionMetric.sparks, MissionScope.run, 600, 450, 100),
    MissionTemplate('sparks_total_1500', 'Collect 1,500 sparks in total', MissionMetric.sparks, MissionScope.total, 1500, 300, 80),
    MissionTemplate('run_1km', 'Run 1 km in one run', MissionMetric.distance, MissionScope.run, 1000, 200, 60),
    MissionTemplate('run_2km', 'Run 2 km in one run', MissionMetric.distance, MissionScope.run, 2000, 350, 100),
    MissionTemplate('run_3km', 'Run 3 km in one run', MissionMetric.distance, MissionScope.run, 3000, 500, 150),
    MissionTemplate('run_total_5km', 'Run 5 km in total', MissionMetric.distance, MissionScope.total, 5000, 300, 80),
    MissionTemplate('slide_15', 'Slide 15 times', MissionMetric.slides, MissionScope.total, 15, 200, 50),
    MissionTemplate('slide_run_8', 'Slide 8 times in one run', MissionMetric.slides, MissionScope.run, 8, 250, 60),
    MissionTemplate('jump_20', 'Jump 20 times', MissionMetric.jumps, MissionScope.total, 20, 200, 50),
    MissionTemplate('jump_run_10', 'Jump 10 times in one run', MissionMetric.jumps, MissionScope.run, 10, 250, 60),
    MissionTemplate('lanes_50', 'Change lanes 50 times', MissionMetric.laneChanges, MissionScope.total, 50, 150, 40),
    MissionTemplate('lanes_run_25', 'Change lanes 25 times in one run', MissionMetric.laneChanges, MissionScope.run, 25, 250, 60),
    MissionTemplate('powerups_3', 'Pick up 3 power-ups', MissionMetric.powerUps, MissionScope.total, 3, 200, 50),
    MissionTemplate('magnet_1', 'Pick up a magnet', MissionMetric.magnet, MissionScope.total, 1, 150, 40),
    MissionTemplate('shield_1', 'Pick up a shield', MissionMetric.shield, MissionScope.total, 1, 150, 40),
    MissionTemplate('double_1', 'Pick up Double Sparks', MissionMetric.doubleSparks, MissionScope.total, 1, 150, 40),
    MissionTemplate('play_3', 'Play 3 runs', MissionMetric.runs, MissionScope.total, 3, 150, 40),
    MissionTemplate('play_5', 'Play 5 runs', MissionMetric.runs, MissionScope.total, 5, 250, 60),
    MissionTemplate('daily_1', 'Finish a Daily Run', MissionMetric.dailyRuns, MissionScope.total, 1, 300, 80),
    MissionTemplate('revive_1', 'Keep running once after a crash', MissionMetric.revives, MissionScope.total, 1, 150, 40),
    MissionTemplate('powerups_run_2', 'Pick up 2 power-ups in one run', MissionMetric.powerUps, MissionScope.run, 2, 300, 80),
  ];

  static MissionTemplate byId(String id) => all.firstWhere((m) => m.id == id);
}

/// One active mission slot.
class Mission {
  Mission({required this.templateId, this.progress = 0, this.claimable = false});

  final String templateId;
  int progress;
  bool claimable;

  MissionTemplate get template => MissionPool.byId(templateId);
  double get fraction => math.min(1.0, progress / template.target);

  Map<String, Object?> toJson() => {'id': templateId, 'progress': progress, 'claimable': claimable};

  factory Mission.fromJson(Map<String, Object?> j) =>
      Mission(templateId: j['id'] as String, progress: (j['progress'] as num?)?.toInt() ?? 0, claimable: j['claimable'] as bool? ?? false);
}

abstract final class Missions {
  static const activeCount = 3;

  /// Applies a run to the active missions. Returns the ids that just became claimable.
  static List<String> apply(List<Mission> active, RunResult run) {
    final completed = <String>[];
    for (final m in active) {
      if (m.claimable) continue;
      final t = m.template;
      final v = t.valueFrom(run);
      m.progress = t.scope == MissionScope.run ? math.max(m.progress, v) : m.progress + v;
      if (m.progress >= t.target) {
        m.progress = t.target;
        m.claimable = true;
        completed.add(t.id);
      }
    }
    return completed;
  }

  /// Picks a template not currently active (and not the one just finished), deterministically from [counter].
  static String next(List<Mission> active, int counter, {String? exclude}) {
    final taken = {for (final m in active) m.templateId, ?exclude};
    final options = MissionPool.all.where((t) => !taken.contains(t.id)).toList();
    return options[math.Random(counter * 7919 + 17).nextInt(options.length)].id;
  }

  static List<Mission> starting() => [
        Mission(templateId: 'run_1km'),
        Mission(templateId: 'sparks_run_300'),
        Mission(templateId: 'slide_15'),
      ];
}
