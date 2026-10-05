import 'dart:async';

import 'package:urban_runner/core/bridge/unity_transport.dart';

/// Records what Flutter sends and lets a test play Unity.
class FakeTransport implements UnityTransport {
  final sent = <String>[];
  int pauses = 0;
  int resumes = 0;
  final _controller = StreamController<String>.broadcast(sync: true);

  @override
  void send(String json) => sent.add(json);

  @override
  void pause() => pauses++;

  @override
  void resume() => resumes++;

  @override
  Stream<String> get messages => _controller.stream;

  @override
  void deliver(String json) => _controller.add(json);
}
