import 'package:flutter/material.dart';

import 'core/bridge/unity_bridge.dart';
import 'core/bridge/unity_transport.dart';
import 'features/spike/spike_screen.dart';

void main() {
  // One bridge for the whole app: Unity is loaded once and never unloaded.
  final bridge = UnityBridge(EmbedUnityTransport());
  runApp(SpikeApp(bridge: bridge));
}

class SpikeApp extends StatelessWidget {
  const SpikeApp({super.key, required this.bridge});

  final UnityBridge bridge;

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Urban Runner',
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(
          seedColor: const Color(0xFFFF3DA6),
          brightness: Brightness.dark,
        ).copyWith(surface: const Color(0xFF0E1230)),
      ),
      home: Builder(
        builder: (context) => Scaffold(
          appBar: AppBar(title: const Text('Urban Runner — M0 spike')),
          body: Center(
            child: FilledButton(
              key: const Key('open_spike'),
              onPressed: () => Navigator.of(context).push(
                MaterialPageRoute<void>(builder: (_) => SpikeScreen(bridge: bridge)),
              ),
              child: const Text('Open Unity spike'),
            ),
          ),
        ),
      ),
    );
  }
}
