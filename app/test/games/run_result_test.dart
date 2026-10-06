import 'package:flutter_test/flutter_test.dart';
import 'package:urban_runner/games/urban_runner/run_result.dart';

/// Negative test N19: session.ended is untrusted input; parsing must never throw.
void main() {
  test('wrong types and missing fields fall back to safe defaults', () {
    final r = RunResult.fromPayload('s', {
      'mode': 42,
      'seed': 'abc',
      'distanceM': '1200',
      'sparks': -7,
      'durationS': null,
      'cause': ['x'],
      'revivesUsed': 99,
      'stats': [1, 2, 3],
      'tutorialCompleted': 'yes',
    });
    expect(r.mode, 'normal');
    expect(r.seed, 0);
    expect(r.distanceM, 0);
    expect(r.sparks, 0);
    expect(r.durationS, 0);
    expect(r.cause, 'obstacle');
    expect(r.revivesUsed, 10);
    expect(r.stats.jumps, 0);
    expect(r.tutorialCompleted, isFalse);
  });

  test('huge, negative and fractional numbers are clamped', () {
    final r = RunResult.fromPayload('s', {'distanceM': 1e300, 'sparks': 1e300, 'stats': {'jumps': -3, 'slides': 2.9}});
    expect(r.distanceM, RunResult.maxDistanceM);
    expect(r.sparks, 100000000);
    expect(r.stats.jumps, 0);
    expect(r.stats.slides, 2);
  });

  test('an empty payload is a valid zero run', () {
    final r = RunResult.fromPayload('s', const {});
    expect(r.distanceM, 0);
    expect(r.mode, 'normal');
  });
}
