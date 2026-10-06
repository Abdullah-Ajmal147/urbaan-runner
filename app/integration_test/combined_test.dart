import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:urban_runner/core/storage/local_store.dart';
import 'package:urban_runner/games/urban_runner/game_state.dart';
import 'package:urban_runner/main.dart' as app;

/// HANDOVER §9 M3 exit, "combined smoke test": the real app with the real embedded Unity on the emulator,
/// 3 complete runs of Home → Game → Results → Home. Unity's bot plays (bridge testMode.autoplay).
/// Needs the exported unityLibrary (tools/build_all.sh emulator) and runs from tools/emulator_smoke.sh combined.
void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  const runSeconds = 25;

  Future<void> pumpUntil(WidgetTester tester, Finder f, {required Duration timeout, String? what}) async {
    final end = DateTime.now().add(timeout);
    while (DateTime.now().isBefore(end)) {
      await tester.pump(const Duration(milliseconds: 250));
      if (f.evaluate().isNotEmpty) return;
    }
    fail('timed out after ${timeout.inSeconds} s waiting for ${what ?? f}');
  }

  testWidgets('3 runs: Home → Game (real Unity, autoplay) → Results → Home', (tester) async {
    await (await SharedPreferences.getInstance()).clear();
    tester.platformDispatcher.accessibilityFeaturesTestValue = const FakeAccessibilityFeatures(disableAnimations: true);
    addTearDown(tester.platformDispatcher.clearAccessibilityFeaturesTestValue);

    await app.runUrbanRunner(testMode: {'autoplay': true, 'skill': 1.0, 'endAfterSeconds': runSeconds});
    await pumpUntil(tester, find.byKey(const Key('onboarding_next')), timeout: const Duration(seconds: 15), what: 'onboarding');
    for (var i = 0; i < 3; i++) {
      await tester.tap(find.byKey(const Key('onboarding_next')));
      await tester.pumpAndSettle();
    }

    for (var run = 1; run <= 3; run++) {
      await pumpUntil(tester, find.byKey(const Key('play')), timeout: const Duration(seconds: 10), what: 'Home (run $run)');
      final started = DateTime.now();
      await tester.tap(find.byKey(const Key('play')));
      // The first run includes Unity's cold start; later runs reuse the loaded player.
      await pumpUntil(tester, find.text('metres'), timeout: const Duration(seconds: runSeconds + 60), what: 'Results (run $run)');
      debugPrint('UR_PERF combined_run=$run home_to_results_s=${DateTime.now().difference(started).inSeconds}');
      await tester.pump(const Duration(seconds: 2));
      await tester.tap(find.bySemanticsLabel('Home'));
      await tester.pumpAndSettle(const Duration(milliseconds: 100), EnginePhase.sendSemanticsUpdate, const Duration(seconds: 10));
    }

    final doc = await (await PrefsLocalStore.open()).read(GameState.profileKey);
    expect(doc!['totalRuns'], 3);
    expect((doc['processedSessions'] as List).length, 3, reason: 'each session rewarded exactly once');
    expect(doc['tutorialDone'], isTrue);
    expect((doc['bestDistance'] as num) > 100, isTrue, reason: 'the bot ran ${runSeconds}s at least twice in normal mode');
    expect(doc['sparks'] as num, greaterThan(0));
  });
}
