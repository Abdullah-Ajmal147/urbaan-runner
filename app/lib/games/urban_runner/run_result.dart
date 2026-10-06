/// Facts Unity reports in session.ended (bridge contract v2). Flutter turns them into rewards.
class RunStats {
  const RunStats({this.jumps = 0, this.slides = 0, this.laneChanges = 0, this.magnet = 0, this.shield = 0, this.doubleSparks = 0});

  final int jumps, slides, laneChanges, magnet, shield, doubleSparks;

  int get powerUps => magnet + shield + doubleSparks;

  factory RunStats.fromJson(Object? raw) {
    final j = raw is Map ? raw : null; // tolerate a malformed payload (negative test N19)
    int v(String k) => _count(j?[k]);
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
    this.huntFound = false,
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
  /// v3: this run's Lantern Hunt lantern was picked up.
  final bool huntFound;

  bool get isDaily => mode == 'daily';

  /// Never throws: Unity's payload is untrusted input. Wrong types fall back to defaults, numbers are made
  /// finite and clamped to sane ranges (a broken result must never leave the player stuck on the game screen).
  factory RunResult.fromPayload(String sessionId, Map<String, Object?> p) => RunResult(
        sessionId: sessionId,
        mode: switch (p['mode']) { 'normal' || 'daily' || 'tutorial' => p['mode'] as String, _ => 'normal' },
        seed: _count(p['seed'], max: 0xFFFFFFFF),
        distanceM: _metres(p['distanceM'], max: maxDistanceM),
        sparks: _count(p['sparks']),
        durationS: _metres(p['durationS'], max: 24 * 3600),
        cause: p['cause'] is String ? p['cause'] as String : 'obstacle',
        revivesUsed: _count(p['revivesUsed'], max: 10),
        stats: RunStats.fromJson(p['stats']),
        tutorialCompleted: p['tutorialCompleted'] == true,
        huntFound: p['huntFound'] == true,
      );

  /// The same limits as [RunResult.fromPayload], for results built in code (defence in depth).
  RunResult sanitised() => RunResult(
        sessionId: sessionId,
        mode: mode,
        seed: seed,
        distanceM: _metres(distanceM, max: maxDistanceM),
        sparks: _count(sparks),
        durationS: _metres(durationS, max: 24 * 3600),
        cause: cause,
        revivesUsed: _count(revivesUsed, max: 10),
        stats: stats,
        tutorialCompleted: tutorialCompleted,
        huntFound: huntFound,
      );

  /// 1,000 km: far beyond any real run, but keeps every later calculation finite.
  static const maxDistanceM = 1000000.0;
}

double _metres(Object? v, {required double max}) {
  if (v is! num) return 0;
  final d = v.toDouble();
  if (!d.isFinite || d < 0) return 0;
  return d > max ? max : d;
}

int _count(Object? v, {int max = 100000000}) {
  if (v is! num) return 0;
  final d = v.toDouble();
  if (!d.isFinite || d < 0) return 0;
  return d > max ? max : d.floor();
}
