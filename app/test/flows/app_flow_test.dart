import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:urban_runner/core/storage/local_store.dart';
import 'package:urban_runner/features/home/hunt_card.dart';
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

  void tallScreen(WidgetTester tester) {
    tester.view.physicalSize = const Size(1080, 2400);
    tester.view.devicePixelRatio = 2.75;
    addTearDown(tester.view.reset);
  }

  Future<void> claimGiftIfOffered(WidgetTester tester) async {
    if (find.byKey(const Key('claim_streak')).evaluate().isEmpty) return;
    await tester.tap(find.byKey(const Key('claim_streak')));
    await tester.pump();
    await tester.pump(const Duration(seconds: 2));
    await tester.pumpAndSettle();
  }

  /// Fresh install → onboarding → tutorial → Home, then the app restarts: the second app session,
  /// the first one where ads may appear (§7.4).
  Future<TestHarness> secondSession(WidgetTester tester) async {
    final store = MemoryLocalStore();
    final first = TestHarness(store: store);
    await boot(tester, first);
    await onboard(tester);
    await playOnce(tester);
    await tester.tap(find.bySemanticsLabel('Home'));
    await tester.pumpAndSettle();
    await tester.pumpWidget(const SizedBox());
    final h = TestHarness(store: store);
    await boot(tester, h);
    await claimGiftIfOffered(tester);
    return h;
  }

  testWidgets('regression: a hanging consent check holds onboarding for at most 3 s (D37)', (tester) async {
    final h = TestHarness();
    h.ads.consentResult = Completer<void>().future; // never answers, like a stalled UMP request
    await boot(tester, h);
    for (var i = 0; i < 2; i++) {
      await tester.tap(find.byKey(const Key('onboarding_next')));
      await tester.pumpAndSettle();
    }
    await tester.tap(find.byKey(const Key('onboarding_next')));
    await tester.pump(const Duration(milliseconds: 100));
    expect(find.text('One moment…'), findsOneWidget);
    await tester.pump(const Duration(seconds: 3));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('onboarding_next')), findsNothing, reason: 'onboarding finished despite the stalled consent check');
  });

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
    expect(find.textContaining('Best '), findsNothing, reason: 'a lesson never claims a best');
    expect(find.textContaining('Lesson done'), findsOneWidget);
    expect(find.textContaining('0 min'), findsNothing);

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

  testWidgets('regression: the first session offers no ad-revive, even when the courier crashes', (tester) async {
    final h = TestHarness();
    await boot(tester, h);
    await onboard(tester);
    await playOnce(tester); // tutorial
    await tester.tap(find.bySemanticsLabel('Home'));
    await tester.pumpAndSettle();
    h.game.requestReviveAd = true; // Unity would offer "Keep running?" if Flutter allowed it
    await playOnce(tester);
    expect(h.game.received.where((m) => m.type == 'ad.result'), isEmpty);
    expect(h.ads.rewardedShown, 0, reason: 'no ads in the tutorial or the first session (§7.4)');
    expect(find.text('metres'), findsOneWidget);
  });

  testWidgets('revive: Unity asks for an ad, Flutter shows it, grants once, the run continues', (tester) async {
    tallScreen(tester);
    final h = await secondSession(tester);

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

  testWidgets('regression: the first session offers no ad-revive, even when the courier crashes', (tester) async {
    final h = TestHarness();
    await boot(tester, h);
    await onboard(tester);
    await playOnce(tester); // tutorial
    await tester.tap(find.bySemanticsLabel('Home'));
    await tester.pumpAndSettle();
    h.game.requestReviveAd = true; // Unity would offer "Keep running?" if Flutter allowed it
    await playOnce(tester);
    expect(h.game.received.where((m) => m.type == 'ad.result'), isEmpty);
    expect(h.ads.rewardedShown, 0, reason: 'no ads in the tutorial or the first session (§7.4)');
    expect(find.text('metres'), findsOneWidget);
  });

  testWidgets('revive: Unity asks for an ad, Flutter shows it, grants once, the run continues', (tester) async {
    tallScreen(tester);
    final h = await secondSession(tester);

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

  testWidgets('interstitials: none in the first session; a missing ad does not start the cool-down (M5)', (tester) async {
    tester.view.physicalSize = const Size(1080, 2400);
    tester.view.devicePixelRatio = 2.75;
    addTearDown(tester.view.reset);
    Future<void> claimGiftIfOffered() async {
      if (find.byKey(const Key('claim_streak')).evaluate().isEmpty) return;
      await tester.tap(find.byKey(const Key('claim_streak')));
      await tester.pump();
      await tester.pump(const Duration(seconds: 2));
      await tester.pumpAndSettle();
    }

    final store = MemoryLocalStore();
    final h = TestHarness(store: store);
    await boot(tester, h);
    await onboard(tester);
    for (var i = 0; i < 4; i++) {
      await playOnce(tester); // tutorial + 3 normal runs, all in the first app session
      await tester.tap(find.bySemanticsLabel('Home'));
      await tester.pumpAndSettle();
      await claimGiftIfOffered();
    }
    expect(h.ads.interstitialsShown, 0, reason: 'never in the first session or the tutorial (§7.4)');

    // Second app session, past interstitial_first_after_runs: the first interstitial is due.
    final h2 = TestHarness(store: store);
    await tester.pumpWidget(const SizedBox());
    await boot(tester, h2);
    await claimGiftIfOffered();
    h2.ads.interstitialReady = false; // no fill / offline
    await playOnce(tester);
    await tester.tap(find.bySemanticsLabel('Home'));
    await tester.pumpAndSettle();
    expect(h2.ads.interstitialsShown, 0);
    expect((await store.read(GameState.profileKey))!['lastInterstitialAtMs'], 0, reason: 'regression: an ad nobody saw must not count');
    expect(h2.analytics.named('ad_interstitial_show'), isEmpty);

    h2.ads.interstitialReady = true; // it loaded meanwhile: the very next exit shows it
    await playOnce(tester);
    await tester.tap(find.bySemanticsLabel('Home'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 50));
    expect(find.textContaining('Interstitial test ad'), findsOneWidget);
    await tester.pump(const Duration(seconds: 2));
    await tester.pumpAndSettle();
    expect(h2.ads.interstitialsShown, 1);
    expect((await store.read(GameState.profileKey))!['lastInterstitialAtMs'], greaterThan(0));
    expect(h2.game.pauses, greaterThanOrEqualTo(1), reason: 'Unity paused for the full-screen ad');
  });

  testWidgets('negative: the revive ad fails → no revive, the run ends and Results still show', (tester) async {
    tallScreen(tester);
    final h = await secondSession(tester);

    h.ads.grant = false; // the ad couldn't load / was closed early
    h.game.requestReviveAd = true;
    await tester.tap(find.byKey(const Key('play')));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));
    await tester.pump(const Duration(seconds: 2));
    await tester.pump(const Duration(seconds: 1));
    await tester.pumpAndSettle(const Duration(milliseconds: 100), EnginePhase.sendSemanticsUpdate, const Duration(seconds: 10));

    final result = h.game.received.where((m) => m.type == 'ad.result').single;
    expect(result.payload['granted'], isFalse);
    expect(h.analytics.named('ad_rewarded_fail'), hasLength(1));
    expect(h.game.resumes, 1, reason: 'Unity is resumed even when the ad fails');
    expect(find.text('metres'), findsOneWidget, reason: 'the player lands on Results, not stuck');
  });

  testWidgets('negative: Home and Results settle (no endless animation draining the battery)', (tester) async {
    final h = TestHarness();
    await tester.pumpWidget(h.app()); // animations ON, unlike the other flows
    await tester.pump(const Duration(seconds: 1));
    // pumpAndSettle throws if something keeps scheduling frames for 30 s.
    for (var i = 0; i < 3; i++) {
      await tester.tap(find.byKey(const Key('onboarding_next')));
      await tester.pumpAndSettle(const Duration(milliseconds: 100), EnginePhase.sendSemanticsUpdate, const Duration(seconds: 30));
    }
    await tester.pumpAndSettle(const Duration(milliseconds: 100), EnginePhase.sendSemanticsUpdate, const Duration(seconds: 30));
    h.game.nextResult = {'distanceM': 900.0};
    await tester.tap(find.byKey(const Key('play')));
    await tester.pump();
    await tester.pump(const Duration(seconds: 2));
    await tester.pumpAndSettle(const Duration(milliseconds: 100), EnginePhase.sendSemanticsUpdate, const Duration(seconds: 30));
    expect(find.text('metres'), findsOneWidget);
  });

  testWidgets('negative: Back on a tab returns to Home instead of closing the app', (tester) async {
    final h = TestHarness();
    await boot(tester, h);
    await onboard(tester);
    await tester.tap(find.text('Store').last);
    await tester.pumpAndSettle();
    expect(find.text('Restore purchases'), findsWidgets);
    final handled = await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();
    expect(handled, isTrue, reason: 'the app consumed Back instead of exiting');
    expect(find.byKey(const Key('play')), findsOneWidget, reason: 'back on Home');
  });

  testWidgets('negative: a day passes while the app is in the background → Home shows the new countdown', (tester) async {
    tester.view.physicalSize = const Size(1080, 2400); // a phone: the Daily card is on screen
    tester.view.devicePixelRatio = 2.75;
    addTearDown(tester.view.reset);
    var now = DateTime.utc(2026, 10, 5, 12, 30); // 11h30 left in the UTC day
    final h = TestHarness(clock: () => now);
    await boot(tester, h);
    await onboard(tester);
    expect(find.text('11h left'), findsOneWidget);
    now = DateTime.utc(2026, 10, 6, 15, 30); // next day, 8h30 left
    tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.paused);
    tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.resumed);
    await tester.pumpAndSettle();
    expect(find.text('8h left'), findsOneWidget);
    expect(find.text('11h left'), findsNothing);
  });

  testWidgets('daily streak: offered on Home after the tutorial, claimed once, flame shows the streak', (tester) async {
    tester.view.physicalSize = const Size(1080, 2400);
    tester.view.devicePixelRatio = 2.75;
    addTearDown(tester.view.reset);
    final store = MemoryLocalStore();
    var h = TestHarness(store: store);
    await boot(tester, h);
    await onboard(tester);
    expect(find.byKey(const Key('claim_streak')), findsNothing, reason: 'never in the first minute (tutorial first)');
    await playOnce(tester);
    await tester.tap(find.bySemanticsLabel('Home'));
    await tester.pumpAndSettle();

    // Next app start: the gift is waiting.
    await tester.pumpWidget(const SizedBox());
    h = TestHarness(store: store);
    await boot(tester, h);
    expect(find.text('Day 1 streak'), findsOneWidget);
    await tester.tap(find.byKey(const Key('claim_streak')));
    await tester.pump();
    await tester.pump(const Duration(seconds: 2));
    await tester.pumpAndSettle();
    expect(find.text('Day 1 streak'), findsNothing, reason: 'sheet closed itself');
    expect(find.bySemanticsLabel('Daily streak, 1 days'), findsOneWidget);
    final doc = await store.read(GameState.profileKey);
    expect(doc!['streak'], 1);
    expect(h.analytics.named('streak_claim'), hasLength(1));
  });

  testWidgets('Lantern Hunt: Unity gets the colour, a found lantern shows on Results and on Home', (tester) async {
    tester.view.physicalSize = const Size(1080, 2400);
    tester.view.devicePixelRatio = 2.75;
    addTearDown(tester.view.reset);
    final h = TestHarness();
    await boot(tester, h);
    await onboard(tester);
    await playOnce(tester); // tutorial: no lantern
    expect(h.game.received.firstWhere((m) => m.type == 'session.start').payload.containsKey('hunt'), isFalse);
    await tester.tap(find.bySemanticsLabel('Home'));
    await tester.pumpAndSettle();
    expect(find.text('Lantern Hunt'), findsOneWidget);

    h.game.nextResult = {'huntFound': true};
    await playOnce(tester);
    final start = h.game.received.where((m) => m.type == 'session.start').last.payload;
    expect(start['hunt'], {'colour': 0, 'found': 0});
    expect(find.text('Lantern found · 1 of 5'), findsOneWidget);
    await tester.tap(find.bySemanticsLabel('Home'));
    await tester.pumpAndSettle();
    // Home's card shows one lit lantern.
    expect(tester.widget<HuntLanterns>(find.byType(HuntLanterns)).found, 1);
  });

  testWidgets('loading: a branded cover (no spinner) until Unity says the run started, then it is gone', (tester) async {
    final h = TestHarness(game: FakeUnityGame(runDuration: const Duration(seconds: 5)));
    await boot(tester, h);
    await onboard(tester);
    await tester.tap(find.byKey(const Key('play')));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 20)); // the game route builds
    expect(find.byKey(const Key('loading_cover')), findsOneWidget);
    expect(find.text('Heading to Lantern City…'), findsOneWidget);
    expect(find.byType(CircularProgressIndicator), findsNothing);
    await tester.pump(const Duration(milliseconds: 120)); // unity.ready → session.start → session.started
    await tester.pump(const Duration(milliseconds: 400)); // fade
    await tester.pump(const Duration(milliseconds: 50));
    expect(find.byKey(const Key('loading_cover')), findsNothing);
    expect(find.byKey(const Key('fake_game')), findsOneWidget);
    await tester.pump(const Duration(seconds: 6)); // let the run finish before the test ends
    await tester.pumpAndSettle(const Duration(milliseconds: 100), EnginePhase.sendSemanticsUpdate, const Duration(seconds: 10));
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
