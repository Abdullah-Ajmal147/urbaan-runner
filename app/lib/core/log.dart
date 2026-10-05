import 'package:flutter/foundation.dart';

/// Tagged logs, shared with Unity so one `adb logcat | grep UR_` shows both sides.
abstract final class Log {
  static const game = 'UR_GAME';
  static const bridge = 'UR_BRIDGE';
  static const perf = 'UR_PERF';
  static const error = 'UR_ERROR';

  /// Tests can redirect logs here.
  static void Function(String tag, String message) sink = _print;

  static void _print(String tag, String message) => debugPrint('$tag $message');

  static void write(String tag, String message) => sink(tag, message);
}
