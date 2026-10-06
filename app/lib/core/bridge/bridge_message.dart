import 'dart:convert';

/// Bridge contract version (docs/bridge-contract.md). Bump on any contract change, on both sides.
const int kBridgeVersion = 2; // v2: session.start.bestDistance, session.ended.tutorialCompleted

class BridgeFormatException implements Exception {
  BridgeFormatException(this.message);
  final String message;
  @override
  String toString() => 'BridgeFormatException: $message';
}

/// One bridge message: `{ "v": 1, "type": "...", "sessionId": "...", "payload": { } }`.
class BridgeMessage {
  BridgeMessage(this.type, {this.sessionId, Map<String, Object?>? payload, this.v = kBridgeVersion})
      : payload = payload ?? const {};

  final int v;
  final String type;
  final String? sessionId;
  final Map<String, Object?> payload;

  /// Dart's jsonEncode always writes numbers with a dot, so the invariant-culture rule holds.
  String encode() => jsonEncode({
        'v': v,
        'type': type,
        if (sessionId != null) 'sessionId': sessionId,
        'payload': payload,
      });

  static BridgeMessage decode(String json) {
    final Object? raw;
    try {
      raw = jsonDecode(json);
    } on FormatException catch (e) {
      throw BridgeFormatException('invalid json: ${e.message}');
    }
    if (raw is! Map<String, Object?>) throw BridgeFormatException('not an object');
    final v = raw['v'];
    if (v is! int) throw BridgeFormatException("missing or non-integer 'v'");
    final type = raw['type'];
    if (type is! String || type.isEmpty) throw BridgeFormatException("missing 'type'");
    final sid = raw['sessionId'];
    if (sid != null && sid is! String) throw BridgeFormatException("'sessionId' must be a string");
    final payload = raw['payload'];
    if (payload != null && payload is! Map<String, Object?>) {
      throw BridgeFormatException("'payload' must be an object");
    }
    return BridgeMessage(type, sessionId: sid as String?, payload: payload as Map<String, Object?>?, v: v);
  }

  @override
  String toString() => encode();
}
