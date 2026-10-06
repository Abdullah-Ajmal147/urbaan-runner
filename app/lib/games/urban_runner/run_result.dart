/// Facts Unity reports in session.ended (bridge contract v2). Flutter turns them into rewards.
class RunStats {
  const RunStats({this.jumps = 0, this.slides = 0, this.laneChanges = 0, this.magnet = 0, this.shield = 0, this.doubleSparks = 0});

  final int jumps, slides, laneChanges, magnet, shield, doubleSparks;

  int get powerUps => magnet + shield + doubleSparks;

  factory RunStats.fromJson(Map<String, Object?>? j) {
    int v(String k) => (j?[k] as num?)?.toInt() ?? 0;
    return RunStats(jumps: v('jumps'), slides: v('slides'), laneChanges: v('laneChanges'), magnet: v('magnet'), shield: v('shield'), doubleSparks: v('doubleSparks'));
  }
}

class RunResult {
  const RunResult({
    required this.sessionId,
    required this.mode,
    required this.seed,
    required this.distanceM,
    required this.sparks,
    required this.durationS,
    required this.cause,
    required this.revivesUsed,
    required this.stats,
    this.tutorialCompleted = false,
  });

  final String sessionId;
  final String mode;
  final int seed;
  final double distanceM;
  final int sparks;
  final double durationS;
  final String cause;
  final int revivesUsed;
  final RunStats stats;
  final bool tutorialCompleted;

  bool get isDaily => mode == 'daily';

  factory RunResult.fromPayload(String sessionId, Map<String, Object?> p) => RunResult(
        sessionId: sessionId,
        mode: p['mode'] as String? ?? 'normal',
        seed: (p['seed'] as num?)?.toInt() ?? 0,
        distanceM: (p['distanceM'] as num?)?.toDouble() ?? 0,
        sparks: (p['sparks'] as num?)?.toInt() ?? 0,
        durationS: (p['durationS'] as num?)?.toDouble() ?? 0,
        cause: p['cause'] as String? ?? 'obstacle',
        revivesUsed: (p['revivesUsed'] as num?)?.toInt() ?? 0,
        stats: RunStats.fromJson(p['stats'] as Map<String, Object?>?),
        tutorialCompleted: p['tutorialCompleted'] as bool? ?? false,
      );
}
