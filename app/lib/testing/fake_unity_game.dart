import 'dart:async';
import 'dart:convert';

import '../core/bridge/bridge_message.dart';
import '../core/bridge/unity_transport.dart';

/// Plays Unity's side of the bridge for widget/integration tests (HANDOVER §9 M3 "FakeUnityGame").
/// Scriptable: what each session ends with, whether it asks for a revive ad, duplicate messages.
class FakeUnityGame implements UnityTransport {
  FakeUnityGame({this.readyDelay = const Duration(milliseconds: 50), this.runDuration = const Duration(milliseconds: 200)});

  final Duration readyDelay;
  final Duration runDuration;
  final _out = StreamController<String>.broadcast();
  final received = <BridgeMessage>[];
  int pauses = 0, resumes = 0;
  bool _ready = false;

  /// Result for the next session (merged over defaults).
  Map<String, Object?> nextResult = {};

  /// Ask for a revive ad mid-run and continue only if granted.
  bool requestReviveAd = false;

  /// Send session.ended twice (bridge rule 2: rewards must still apply once).
  bool duplicateEnded = false;

  /// Never answer (tests the 10 s unity.ready timeout).
  bool silent = false;

  String? _sessionId;
  Map<String, Object?> _startPayload = {};

  /// Simulates the embed widget appearing: Unity loads and announces itself once per process.
  void attach() {
    if (_ready || silent) return;
    Timer(readyDelay, () {
      _ready = true;
      _emit(BridgeMessage('unity.ready', payload: {'bridgeVersion': kBridgeVersion, 'buildVersion': 'fake'}));
    });
  }

  @override
  Stream<String> get messages => _out.stream;

  @override
  void deliver(String json) => _out.add(json);

  void _emit(BridgeMessage m) => deliver(m.encode());

  @override
  void pause() => pauses++;

  @override
  void resume() => resumes++;

  @override
  void send(String json) {
    final m = BridgeMessage.decode(json);
    received.add(m);
    switch (m.type) {
      case 'session.start':
        _sessionId = m.sessionId;
        _startPayload = m.payload;
        _emit(BridgeMessage('session.started', sessionId: _sessionId));
        if (requestReviveAd) {
          Timer(runDuration ~/ 2, () {
            _emit(BridgeMessage('analytics.event', sessionId: _sessionId, payload: {'name': 'revive_offer', 'params': {'session_id': _sessionId}}));
            _emit(BridgeMessage('ad.request', sessionId: _sessionId, payload: {'placement': 'revive'}));
          });
        } else {
          Timer(runDuration, _end);
        }
      case 'ad.result':
        Timer(runDuration ~/ 2, () => _end(revived: m.payload['granted'] == true));
      default:
        break;
    }
  }

  void _end({bool revived = false}) {
    final payload = <String, Object?>{
      'mode': _startPayload['mode'] ?? 'normal',
      'seed': _startPayload['seed'] ?? 1,
      'distanceM': 812.4,
      'sparks': 96,
      'durationS': 61.5,
      'cause': 'obstacle',
      'revivesUsed': revived ? 1 : 0,
      'stats': {'jumps': 9, 'slides': 6, 'laneChanges': 21, 'magnet': 1, 'shield': 0, 'doubleSparks': 0},
      'buildVersion': 'fake',
      'tutorialCompleted': _startPayload['mode'] == 'tutorial',
      ...nextResult,
    };
    final ended = BridgeMessage('session.ended', sessionId: _sessionId, payload: payload);
    _emit(ended);
    if (duplicateEnded) _emit(ended);
  }

  Map<String, Object?> get lastStartPayload => jsonDecode(jsonEncode(_startPayload)) as Map<String, Object?>;
}
