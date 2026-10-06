import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:urban_runner/core/widgets/phosphor.dart';
import 'package:provider/provider.dart';
import 'package:uuid/uuid.dart';

import '../../core/app_services.dart';
import '../../core/bridge/bridge_message.dart';
import '../../core/bridge/unity_bridge.dart';
import '../../core/log.dart';
import '../../core/theme/tokens.dart';
import '../../core/widgets/widgets.dart';
import '../../games/urban_runner/game_state.dart';
import '../../games/urban_runner/run_result.dart';
import '../../games/urban_runner/session_payload.dart';
import '../results/results_screen.dart';

/// How a run is started from Home.
enum RunKind { normal, daily }

/// Full-screen Unity with no Flutter UI on top during play (HANDOVER §4.2). Runs exactly one session.
class GameScreen extends StatefulWidget {
  const GameScreen({super.key, this.kind = RunKind.normal, this.testMode});

  final RunKind kind;

  /// Integration tests / combined smoke runs: autoplay etc. (bridge contract testMode).
  final Map<String, Object?>? testMode;

  static Route<void> route({RunKind kind = RunKind.normal, Map<String, Object?>? testMode}) =>
      PageRouteBuilder<void>(
        settings: const RouteSettings(name: '/game'),
        transitionDuration: UrMotion.slow,
        pageBuilder: (_, _, _) => GameScreen(kind: kind, testMode: testMode),
        transitionsBuilder: (_, a, _, child) => FadeTransition(opacity: a, child: child),
      );

  @override
  State<GameScreen> createState() => _GameScreenState();
}

enum _Phase { connecting, failed, running, ended }

class _GameScreenState extends State<GameScreen> {
  late final AppServices s = context.read<AppServices>();
  late final GameState state = context.read<GameState>();
  StreamSubscription<BridgeMessage>? _sub;
  _Phase _phase = _Phase.connecting;
  String? _sessionId;
  String _mode = 'normal';
  bool _rewardedThisRun = false;

  UnityBridge get bridge => s.bridge;

  @override
  void initState() {
    super.initState();
    SystemChrome.setEnabledSystemUIMode(SystemUiMode.immersiveSticky);
    _sub = bridge.messages.listen(_onMessage);
    _connect();
  }

  Future<void> _connect() async {
    setState(() => _phase = _Phase.connecting);
    try {
      await bridge.waitUntilReady();
    } on TimeoutException {
      if (mounted) setState(() => _phase = _Phase.failed);
      return;
    }
    if (!mounted) return;
    _start();
  }

  void _start() {
    final p = state.profile;
    _mode = !p.tutorialDone ? 'tutorial' : widget.kind == RunKind.daily ? 'daily' : 'normal';
    final seed = _mode == 'daily' ? state.dailySeed : DateTime.now().microsecondsSinceEpoch & 0x7FFFFFFF;
    _sessionId = const Uuid().v4();
    bridge.send(BridgeMessage('session.start', sessionId: _sessionId, payload: SessionPayload.build(
      profile: p,
      config: s.config,
      mode: _mode,
      seed: seed,
      testMode: widget.testMode,
    )));
    s.analytics.log('game_start', {'session_id': _sessionId!, 'mode': _mode, 'character': p.selectedCharacter});
    setState(() => _phase = _Phase.running);
  }

  Future<void> _onMessage(BridgeMessage m) async {
    if (m.sessionId != null && m.sessionId != _sessionId && m.type != 'unity.ready') return;
    switch (m.type) {
      case 'analytics.event':
        _forwardAnalytics(m.payload);
      case 'ad.request':
        await _handleAdRequest(m);
      case 'session.ended':
        await _finish(m);
      case 'error':
        Log.write(Log.error, 'unity error ${m.payload}');
      default:
        break;
    }
  }

  /// Unity forwards in-run analytics; Flutter owns the SDK (HANDOVER §5.2). Only known events pass.
  static const _unityEvents = {'revive_offer', 'revive_accept', 'revive_decline', 'tutorial_begin', 'tutorial_complete'};

  void _forwardAnalytics(Map<String, Object?> p) {
    final name = p['name'] as String?;
    if (name == null || !_unityEvents.contains(name)) return;
    final params = <String, Object>{};
    (p['params'] as Map?)?.forEach((k, v) {
      if (v is String || v is num) params[k as String] = v as Object;
    });
    s.analytics.log(name, params);
  }

  Future<void> _handleAdRequest(BridgeMessage m) async {
    final placement = m.payload['placement'] as String? ?? 'revive';
    // Pause Unity before a full-screen ad and resume it afterwards (HANDOVER §7.4).
    bridge.transport.pause();
    s.analytics.log('ad_rewarded_start', {'placement': placement});
    var granted = false;
    try {
      granted = await s.ads.showRewarded(placement);
    } catch (e) {
      Log.write(Log.error, 'rewarded ad failed: $e');
    }
    s.analytics.log(granted ? 'ad_rewarded_complete' : 'ad_rewarded_fail', {'placement': placement});
    if (granted) _rewardedThisRun = true;
    bridge.transport.resume();
    if (!mounted || _phase != _Phase.running) return;
    bridge.send(BridgeMessage('ad.result', sessionId: _sessionId, payload: {'placement': placement, 'granted': granted}));
  }

  Future<void> _finish(BridgeMessage m) async {
    if (_phase == _Phase.ended) return;
    _phase = _Phase.ended;
    final result = RunResult.fromPayload(m.sessionId ?? _sessionId!, m.payload);
    final reward = await state.applyRun(result);
    if (!mounted) return;
    Navigator.of(context).pushReplacement(ResultsScreen.route(reward, rewardedThisRun: _rewardedThisRun));
  }

  /// System back during a run pauses it (Unity shows its pause menu) instead of leaving.
  void _onBack() {
    if (_phase == _Phase.running) {
      bridge.send(BridgeMessage('game.pause', sessionId: _sessionId));
    } else {
      Navigator.of(context).maybePop();
    }
  }

  @override
  void dispose() {
    _sub?.cancel();
    SystemChrome.setEnabledSystemUIMode(SystemUiMode.edgeToEdge);
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return PopScope(
      canPop: _phase == _Phase.failed,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) _onBack();
      },
      child: Scaffold(
        backgroundColor: UrColors.plumDeep,
        body: Stack(fit: StackFit.expand, children: [
          s.gameView(context, bridge),
          if (_phase == _Phase.connecting)
            Center(child: Semantics(label: 'Loading the game', child: const CircularProgressIndicator(color: UrColors.magenta))),
          if (_phase == _Phase.failed)
            Container(
              color: UrColors.night,
              padding: const EdgeInsets.all(24),
              child: Column(mainAxisAlignment: MainAxisAlignment.center, children: [
                Icon(PhosphorIcons.warningCircle(PhosphorIconsStyle.fill), size: 48, color: UrColors.amber),
                const SizedBox(height: 16),
                const Text("The street didn't load", style: UrText.title, textAlign: TextAlign.center),
                const SizedBox(height: 8),
                const Text('Something got stuck while starting the game. It usually works on a second try.', style: UrText.body, textAlign: TextAlign.center),
                const SizedBox(height: 24),
                PressButton(label: 'Try again', icon: PhosphorIcons.arrowClockwise(PhosphorIconsStyle.bold), onPressed: _connect),
                const SizedBox(height: 8),
                TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Back to home', style: UrText.label)),
              ]),
            ),
        ]),
      ),
    );
  }
}
