import 'dart:async';

import 'package:flutter_embed_unity/flutter_embed_unity.dart' as embed;

/// Moves raw JSON between Flutter and Unity. The real one uses flutter_embed_unity;
/// tests use a fake (FakeUnityGame, from M3).
abstract class UnityTransport {
  void send(String json);
  void pause();
  void resume();

  /// Messages from Unity. The embed widget feeds these via [deliver].
  Stream<String> get messages;

  /// Called by the screen hosting the Unity view with each message from Unity.
  void deliver(String json);
}

class EmbedUnityTransport implements UnityTransport {
  static const gameObject = 'FlutterBridge';
  static const method = 'OnFlutterMessage';

  final _controller = StreamController<String>.broadcast();

  @override
  void send(String json) => embed.sendToUnity(gameObject, method, json);

  @override
  void pause() => embed.pauseUnity();

  @override
  void resume() => embed.resumeUnity();

  @override
  Stream<String> get messages => _controller.stream;

  @override
  void deliver(String json) => _controller.add(json);
}
