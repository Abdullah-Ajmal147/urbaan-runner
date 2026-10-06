import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:urban_runner/core/storage/local_store.dart';
import 'package:urban_runner/games/urban_runner/game_state.dart';
import 'package:urban_runner/testing/test_app.dart';

/// HANDOVER §9 M3 exit: on the emulator, with the fake Unity and the real on-device storage,
/// onboarding → play → results → persistence after an app restart.
///   flutter test integration_test/flows_test.dart -d emulator-5554
void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  Future<void> boot(WidgetTester tester, TestHarness h) async {
    tester.platformDispatcher.accessibilityFeaturesTestValue = const FakeAccessibilityFeatures(disableAnimations: true);
    addTearDown(tester.platformDispatcher.clearAccessibilityFeaturesTestValue);
    await tester.pumpWidget(h.app());
    await tester.pumpAndSettle();
  }

  Future<void> playOnce(WidgetTester tester) async {
    await tester.tap(find.byKey(const Key('play')));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 20));
    expect(find.byKey(const Key('fake_game')), findsOneWidget);
    await tester.pump(const Duration(seconds: 2));
    await tester.pumpAndSettle(const Duration(milliseconds: 100), EnginePhase.sendSemanticsUpdate, const Duration(seconds: 10));
  }

  testWidgets('onboarding → tutorial → normal run → results → restart keeps progress (device storage)', (tester) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.clear();

    final h = TestHarness(store: await PrefsLocalStore.open());
    await boot(tester, h);
    expect(find.text('Swipe to run'), findsOneWidget);
    for (var i = 0; i < 3; i++) {
      await tester.tap(find.byKey(const Key('onboarding_next')));
      await tester.pumpAndSettle();
    }

    await playOnce(tester); // tutorial
    expect(find.text("You're ready"), findsOneWidget);
    await tester.tap(find.bySemanticsLabel('Home'));
    await tester.pumpAndSettle();

    h.game.nextResult = {'distanceM': 1450.0, 'sparks': 210};
    await playOnce(tester);
    expect(find.text('New personal best'), findsOneWidget);
    await tester.tap(find.bySemanticsLabel('Home'));
    await tester.pumpAndSettle();

    // Restart: a brand-new app and store instance on the same device storage.
    await tester.pumpWidget(const SizedBox());
    final h2 = TestHarness(store: await PrefsLocalStore.open());
    await boot(tester, h2);
    expect(find.text('Play'), findsOneWidget, reason: 'no onboarding after restart');
    expect(find.textContaining('1,450'), findsWidgets, reason: 'best distance shown on Home');
    final doc = await h2.store.read(GameState.profileKey);
    expect(doc!['bestDistance'], 1450);
    expect(doc['totalRuns'], 2);
    expect(doc['onboardingDone'], isTrue);
  });
}
