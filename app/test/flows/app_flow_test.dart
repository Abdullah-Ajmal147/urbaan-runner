import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:urban_runner/core/storage/local_store.dart';
import 'package:urban_runner/games/urban_runner/game_state.dart';
import 'package:urban_runner/testing/fake_unity_game.dart';
import 'package:urban_runner/testing/test_app.dart';

/// Full-app flows with the fake Unity game (HANDOVER §9 M3, §10.3 #1, #3, #5, #9, #18).
void main() {
  Future<void> boot(WidgetTester tester, TestHarness h) async {
    // Looping decorative animations never "settle"; the system's remove-animations setting stops them
    // (and exercises the Reduce motion path).
    tester.platformDispatcher.accessibilityFeaturesTestValue = const FakeAccessibilityFeatures(disableAnimations: true);
    addTearDown(tester.platformDispatcher.clearAccessibilityFeaturesTestValue);
    await tester.pumpWidget(h.app());
    await tester.pumpAndSettle();
  }

  Future<void> onboard(WidgetTester tester) async {
    for (var i = 0; i < 3; i++) {
      await tester.tap(find.byKey(const Key('onboarding_next')));
      await tester.pumpAndSettle();
    }
  }

  /// Tap Play, let the fake game run and end, land on Results.
  Future<void> playOnce(WidgetTester tester, {Finder? start}) async {
    await tester.tap(start ?? find.byKey(const Key('play')));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 20));
    expect(find.byKey(const Key('fake_game')), findsOneWidget, reason: 'the game screen shows Unity (here: the fake)');
    await tester.pump(const Duration(seconds: 2));
    await tester.pumpAndSettle(const Duration(milliseconds: 100), EnginePhase.sendSemanticsUpdate, const Duration(seconds: 10));
  }

  testWidgets('fresh install → onboarding → first run is the tutorial → results → home', (tester) async {
    final h = TestHarness();
    await boot(tester, h);
    expect(find.text('Swipe to run'), findsOneWidget);
    await onboard(tester);
    expect(find.text('Start your first run'), findsOneWidget);

    await playOnce(tester);
    expect(h.game.received.firstWhere((m) => m.type == 'session.start').payload['mode'], 'tutorial');
    expect(find.text("You're ready"), findsOneWidget);
    expect(find.text('metres'), findsOneWidget);

    await tester.tap(find.bySemanticsLabel('Home'));
    await tester.pumpAndSettle();
    expect(find.text('Play'), findsOneWidget, reason: 'after the tutorial, Play is a normal run');
  });

  testWidgets('a normal run reports, rewards once, shows results; progress persists after restart', (tester) async {
    final store = MemoryLocalStore();
    final h = TestHarness(store: store);
    await boot(tester, h);
    await onboard(tester);
    await playOnce(tester); // tutorial
    await tester.tap(find.bySemanticsLabel('Home'));
    await tester.pumpAndSettle();

    h.game.nextResult = {'distanceM': 1450.0, 'sparks': 210};
    h.game.duplicateEnded = true; // Unity says it twice: rewards must still apply once (rule 2)
    await playOnce(tester);
    final start = h.game.received.where((m) => m.type == 'session.start').last.payload;
    expect(start['mode'], 'normal');
    expect(start['config'], isA<Map<Object?, Object?>>());
    expect(start['bestDistance'], 0, reason: 'tutorial runs never count as a best');
    expect(find.text('New personal best'), findsOneWidget);

    await tester.tap(find.bySemanticsLabel('Home'));
    await tester.pumpAndSettle();

    // Restart the app on the same storage.
    final h2 = TestHarness(store: store);
    await tester.pumpWidget(const SizedBox());
    await boot(tester, h2);
    expect(find.text('Play'), findsOneWidget, reason: 'no onboarding the second time');
    final doc = await store.read(GameState.profileKey);
    expect(doc!['bestDistance'], 1450);
    expect(doc['totalRuns'], 2);
    expect((doc['processedSessions'] as List).length, 2, reason: 'duplicate session.ended counted once');
    expect(find.textContaining('1,450'), findsWidgets);
  });

  testWidgets('revive: Unity asks for an ad, Flutter shows it, grants once, the run continues', (tester) async {
    final h = TestHarness();
    await boot(tester, h);
    await onboard(tester);
    await playOnce(tester);
    await tester.tap(find.bySemanticsLabel('Home'));
    await tester.pumpAndSettle();

    h.game.requestReviveAd = true;
    await tester.tap(find.byKey(const Key('play')));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 100)); // unity.ready → session.start
    await tester.pump(const Duration(milliseconds: 150)); // fake asks for the revive ad
    await tester.pump(const Duration(milliseconds: 50));
    expect(find.textContaining('Rewarded test ad'), findsOneWidget);
    await tester.pump(const Duration(seconds: 2)); // the test ad closes itself
    await tester.pump(const Duration(seconds: 1));
    await tester.pumpAndSettle(const Duration(milliseconds: 100), EnginePhase.sendSemanticsUpdate, const Duration(seconds: 10));

    final result = h.game.received.where((m) => m.type == 'ad.result').single;
    expect(result.payload['granted'], isTrue);
    expect(h.game.pauses, 1, reason: 'Unity paused for the full-screen ad');
    expect(h.game.resumes, 1);
    expect(h.analytics.named('ad_rewarded_complete').single.params['placement'], 'revive');
    expect(h.analytics.named('revive_offer'), hasLength(1), reason: 'forwarded from Unity');
    expect(find.text('metres'), findsOneWidget);
  });

  testWidgets('Unity never answers: error state with Retry after 10 s', (tester) async {
    final h = TestHarness(game: FakeUnityGame()..silent = true);
    await boot(tester, h);
    await onboard(tester);
    await tester.tap(find.byKey(const Key('play')));
    await tester.pump(); // build the game screen: the 10 s ready timer starts now
    await tester.pump(const Duration(seconds: 9));
    expect(find.text("The street didn't load"), findsNothing);
    await tester.pump(const Duration(seconds: 2));
    expect(find.text("The street didn't load"), findsOneWidget);
    expect(find.text('Try again'), findsOneWidget);
  });

  testWidgets('missions: a completed mission can be claimed from the Missions tab', (tester) async {
    final h = TestHarness();
    await boot(tester, h);
    await onboard(tester);
    await playOnce(tester); // tutorial run: 812 m, 9 jumps, 6 slides...
    await tester.tap(find.bySemanticsLabel('Home'));
    await tester.pumpAndSettle();
    h.game.nextResult = {'distanceM': 1100.0};
    await playOnce(tester);
    await tester.tap(find.bySemanticsLabel('Home'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Missions').last);
    await tester.pumpAndSettle();
    expect(find.text('Run 1 km in one run'), findsOneWidget);
    await tester.tap(find.byKey(const Key('claim_0')));
    await tester.pumpAndSettle();
    expect(find.textContaining('A new mission is ready'), findsOneWidget);
    expect(find.text('Run 1 km in one run'), findsNothing);
  });
}
