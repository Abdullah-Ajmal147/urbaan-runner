import 'package:flutter_test/flutter_test.dart';
import 'package:urban_runner/games/urban_runner/economy.dart';
import 'package:urban_runner/games/urban_runner/missions.dart';
import 'package:urban_runner/games/urban_runner/profile.dart';
import 'package:urban_runner/games/urban_runner/run_result.dart';

RunResult run({double distance = 1000, int sparks = 100, String mode = 'normal', RunStats stats = const RunStats(), int revives = 0, String id = 's'}) =>
    RunResult(sessionId: id, mode: mode, seed: 1, distanceM: distance, sparks: sparks, durationS: 60, cause: 'obstacle', revivesUsed: revives, stats: stats);

void main() {
  group('Economy', () {
    test('level curve is monotonic and round-trips', () {
      for (var l = 1; l < 60; l++) {
        expect(Economy.xpForLevel(l + 1), greaterThan(Economy.xpForLevel(l)));
        expect(Economy.levelForXp(Economy.xpForLevel(l)), l);
        expect(Economy.levelForXp(Economy.xpForLevel(l + 1) - 1), l);
      }
    });

    test('sparks above what the distance allows are capped and flagged (bridge rule 3)', () {
      expect(Economy.sanitiseSparks(100, 1000), (100, false));
      final (capped, rejected) = Economy.sanitiseSparks(999999, 100);
      expect(rejected, isTrue);
      expect(capped, 125);
      expect(Economy.sanitiseSparks(-5, 100), (0, true));
    });

    test('multipliers: Remote Config sparks multiplier and live event (rule 8)', () {
      expect(Economy.applyMultipliers(100, sparksMultiplier: 1, eventActive: false, eventMultiplier: 2), 100);
      expect(Economy.applyMultipliers(100, sparksMultiplier: 1.5, eventActive: false, eventMultiplier: 2), 150);
      expect(Economy.applyMultipliers(100, sparksMultiplier: 1.5, eventActive: true, eventMultiplier: 2), 300);
    });
  });

  group('Missions', () {
    test('pool has at least 20 unique templates (HANDOVER §3.7)', () {
      expect(MissionPool.all.length, greaterThanOrEqualTo(20));
      expect(MissionPool.all.map((m) => m.id).toSet().length, MissionPool.all.length);
    });

    test('"in one run" keeps the best single run; "total" adds up', () {
      final runMission = Mission(templateId: 'sparks_run_300');
      final totalMission = Mission(templateId: 'sparks_total_1500');
      Missions.apply([runMission, totalMission], run(sparks: 120));
      Missions.apply([runMission, totalMission], run(sparks: 200));
      expect(runMission.progress, 200);
      expect(totalMission.progress, 320);
      expect(runMission.claimable, isFalse);
      final done = Missions.apply([runMission], run(sparks: 300));
      expect(done, ['sparks_run_300']);
      expect(runMission.claimable, isTrue);
      expect(runMission.progress, 300);
    });

    test('stats feed the right missions', () {
      final ms = [Mission(templateId: 'slide_15'), Mission(templateId: 'magnet_1'), Mission(templateId: 'daily_1')];
      Missions.apply(ms, run(stats: const RunStats(slides: 15, magnet: 1)));
      expect(ms[0].claimable, isTrue);
      expect(ms[1].claimable, isTrue);
      expect(ms[2].claimable, isFalse, reason: 'a normal run is not a Daily Run');
      Missions.apply(ms, run(mode: 'daily'));
      expect(ms[2].claimable, isTrue);
    });

    test('a new mission never duplicates an active one or the one just finished', () {
      final active = Missions.starting();
      for (var c = 0; c < 200; c++) {
        final next = Missions.next(active, c, exclude: 'run_2km');
        expect(active.map((m) => m.templateId), isNot(contains(next)));
        expect(next, isNot('run_2km'));
      }
    });
  });

  group('Daily Run', () {
    test('same seed for everyone on a UTC day, different seed tomorrow', () {
      final a = DailyRun.seedFor(DailyRun.dateKey(DateTime.utc(2026, 10, 5, 0, 1)));
      final b = DailyRun.seedFor(DailyRun.dateKey(DateTime.utc(2026, 10, 5, 23, 59)));
      final c = DailyRun.seedFor(DailyRun.dateKey(DateTime.utc(2026, 10, 6)));
      expect(a, b);
      expect(a, isNot(c));
      expect(DailyRun.dateKey(DateTime.utc(2026, 1, 2)), '2026-01-02');
    });
  });
}
