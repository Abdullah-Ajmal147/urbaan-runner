import 'dart:async';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'core/app_services.dart';
import 'core/log.dart';
import 'core/remote_config/remote_config.dart';
import 'core/storage/local_store.dart';
import 'core/sync/cloud_sync.dart';
import 'core/theme/app_theme.dart';
import 'core/theme/tokens.dart';
import 'core/widgets/widgets.dart';
import 'features/onboarding/onboarding_screen.dart';
import 'features/shell/shell.dart';
import 'features/splash/splash_screen.dart';
import 'features/store/purchases.dart';
import 'games/urban_runner/game_state.dart';
import 'games/urban_runner/profile.dart';
import 'games/urban_runner/profile_sync.dart';

/// App root: starts services in parallel behind the splash (≤ 2 s), then onboarding (first launch) or Home.
/// No login wall: everything works offline from the first launch (HANDOVER §4.1).
class UrbanRunnerApp extends StatefulWidget {
  const UrbanRunnerApp({
    super.key,
    required this.services,
    required this.navigatorKey,
    required this.openStore,
    required this.configSource,
    this.clock,
    this.splashMinimum = const Duration(milliseconds: 600),
  });

  final AppServices services;
  final GlobalKey<NavigatorState> navigatorKey;
  final Future<LocalStore> Function() openStore;
  final RemoteConfigSource configSource;
  final DateTime Function()? clock;
  final Duration splashMinimum;

  @override
  State<UrbanRunnerApp> createState() => _UrbanRunnerAppState();
}

class _UrbanRunnerAppState extends State<UrbanRunnerApp> {
  final _messenger = GlobalKey<ScaffoldMessengerState>();
  GameState? _state;
  Purchases? _purchases;
  CloudSync<PlayerProfile>? _sync;
  bool _onboarded = false;

  AppServices get s => widget.services;

  @override
  void initState() {
    super.initState();
    _boot();
  }

  Future<void> _boot() async {
    final started = DateTime.now();
    final store = await widget.openStore();
    // Remote Config: values fetched last session are activated now; a new fetch is for the next start (§7.3).
    await s.config.activate(widget.configSource);
    unawaited(widget.configSource.fetchForNextStart().catchError((Object e) => Log.write(Log.error, 'config fetch: $e')));
    final state = GameState(store: store, analytics: s.analytics, config: s.config, clock: widget.clock);
    await state.load();
    // Local-first cloud saves (HANDOVER §7.1): never awaited, so the splash never waits on the network.
    final cloud = s.cloud;
    if (cloud != null) {
      final sync = _sync = CloudSync<PlayerProfile>(
        backend: cloud,
        adapter: UrbanRunnerSyncAdapter(),
        readLocal: () => state.profile,
        applyMerged: state.replaceProfile,
      );
      state.onSaved = (_) => sync.schedule();
      unawaited(sync.syncNow());
    }
    final purchases = Purchases(store: s.store, state: state, analytics: s.analytics)
      ..notify = (msg) => _messenger.currentState?.showSnackBar(SnackBar(content: Text(msg)));
    s.sounds
      ..sfx = state.profile.settings.sfx
      ..haptics = state.profile.settings.haptics;
    if (state.profile.onboardingDone) unawaited(s.ads.init());
    // Entitlements come from Play on every start (§7.5); quietly re-grant anything missing.
    unawaited(purchases.restore(quiet: true));
    final elapsed = DateTime.now().difference(started);
    if (elapsed < widget.splashMinimum) await Future<void>.delayed(widget.splashMinimum - elapsed);
    Log.write(Log.perf, 'boot_ms=${DateTime.now().difference(started).inMilliseconds}');
    if (!mounted) return;
    setState(() {
      _state = state;
      _purchases = purchases;
      _onboarded = state.profile.onboardingDone;
    });
  }

  @override
  void dispose() {
    _sync?.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final state = _state;
    // The Navigator must never be rebuilt with a different root: it keeps its first route. So the screen
    // switch (splash → onboarding → home) happens inside one stable home widget that listens to _BootScope.
    return Provider.value(
      value: s,
      child: MaterialApp(
        title: 'Urban Runner',
        debugShowCheckedModeBanner: false,
        theme: buildAppTheme(),
        navigatorKey: widget.navigatorKey,
        scaffoldMessengerKey: _messenger,
        builder: (context, child) {
          final reduce = state?.profile.settings.reduceMotion ?? false;
          // Text can scale up to 1.3× without clipping (HANDOVER §4.1); larger system sizes are capped there.
          final mq = MediaQuery.of(context);
          Widget c = _BootScope(
            booted: state != null,
            onboarded: _onboarded,
            onOnboarded: () => setState(() => _onboarded = true),
            child: MotionScope(reduceMotion: reduce, child: child!),
          );
          if (state != null) {
            c = MultiProvider(providers: [
              ChangeNotifierProvider.value(value: state),
              ChangeNotifierProvider.value(value: _purchases!),
            ], child: c);
          }
          return MediaQuery(data: mq.copyWith(textScaler: mq.textScaler.clamp(maxScaleFactor: 1.3)), child: c);
        },
        home: const _RootGate(),
      ),
    );
  }
}

/// Boot progress for [_RootGate].
class _BootScope extends InheritedWidget {
  const _BootScope({required this.booted, required this.onboarded, required this.onOnboarded, required super.child});
  final bool booted;
  final bool onboarded;
  final VoidCallback onOnboarded;

  static _BootScope of(BuildContext context) => context.dependOnInheritedWidgetOfExactType<_BootScope>()!;

  @override
  bool updateShouldNotify(_BootScope old) => old.booted != booted || old.onboarded != onboarded;
}

/// Splash → onboarding (first launch) → home, without replacing the Navigator's root route.
class _RootGate extends StatelessWidget {
  const _RootGate();

  @override
  Widget build(BuildContext context) {
    final boot = _BootScope.of(context);
    final Widget screen = !boot.booted
        ? const SplashScreen()
        : !boot.onboarded
            ? OnboardingScreen(onDone: boot.onOnboarded)
            : const Shell();
    return AnimatedSwitcher(duration: UrMotion.normal, child: KeyedSubtree(key: ValueKey(screen.runtimeType), child: screen));
  }
}
