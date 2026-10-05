import 'dart:async';

import '../log.dart';
import 'bridge_message.dart';
import 'unity_transport.dart';

enum BridgeState { waiting, ready, timedOut }

/// Flutter's side of the bridge contract (docs/bridge-contract.md):
/// waits for `unity.ready` (rule 1), parses and version-checks everything (rule 5).
class UnityBridge {
  UnityBridge(this.transport, {this.readyTimeout = const Duration(seconds: 10)}) {
    _sub = transport.messages.listen(_onRaw);
  }

  final UnityTransport transport;
  final Duration readyTimeout;

  final _messages = StreamController<BridgeMessage>.broadcast();
  late final StreamSubscription<String> _sub;
  Completer<void>? _ready;
  Timer? _readyTimer;
  BridgeState _state = BridgeState.waiting;
  Map<String, Object?> _readyPayload = const {};

  BridgeState get state => _state;
  Map<String, Object?> get readyPayload => _readyPayload;

  /// Valid messages from Unity, after `unity.ready`.
  Stream<BridgeMessage> get messages => _messages.stream;

  /// Completes when Unity says it's ready; throws [TimeoutException] after [readyTimeout].
  /// Calling again after a timeout restarts the wait (the Retry button).
  Future<void> waitUntilReady() {
    if (_state == BridgeState.ready) return Future.value();
    if (_ready != null && !_ready!.isCompleted) return _ready!.future;
    _state = BridgeState.waiting;
    final c = _ready = Completer<void>();
    _readyTimer?.cancel();
    _readyTimer = Timer(readyTimeout, () {
      if (c.isCompleted) return;
      _state = BridgeState.timedOut;
      Log.write(Log.error, 'unity.ready not received within ${readyTimeout.inSeconds}s');
      c.completeError(TimeoutException('unity.ready', readyTimeout));
    });
    return c.future;
  }

  void send(BridgeMessage message) {
    if (_state != BridgeState.ready) {
      throw StateError('bridge not ready; cannot send ${message.type}');
    }
    final json = message.encode();
    Log.write(Log.bridge, '-> $json');
    transport.send(json);
  }

  void _onRaw(String raw) {
    Log.write(Log.bridge, '<- $raw');
    final BridgeMessage msg;
    try {
      msg = BridgeMessage.decode(raw);
    } on BridgeFormatException catch (e) {
      Log.write(Log.error, 'bad message from unity: ${e.message}');
      return;
    }
    if (msg.v != kBridgeVersion) {
      Log.write(Log.error, 'version mismatch: expected v$kBridgeVersion, got v${msg.v}');
      return;
    }
    if (msg.type == 'unity.ready') {
      _readyPayload = msg.payload;
      final remote = msg.payload['bridgeVersion'];
      if (remote != kBridgeVersion) {
        Log.write(Log.error, 'unity bridgeVersion $remote != $kBridgeVersion');
      }
      _state = BridgeState.ready;
      _readyTimer?.cancel();
      final c = _ready;
      if (c != null && !c.isCompleted) c.complete();
    }
    _messages.add(msg);
  }

  Future<void> dispose() async {
    _readyTimer?.cancel();
    await _sub.cancel();
    await _messages.close();
  }
}
