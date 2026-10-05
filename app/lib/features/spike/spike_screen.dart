import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_embed_unity/flutter_embed_unity.dart';

import '../../core/bridge/bridge_message.dart';
import '../../core/bridge/unity_bridge.dart';
import '../../core/log.dart';

/// M0 bridge spike: embeds Unity, measures ping→pong, shows swipes, pauses/resumes.
class SpikeScreen extends StatefulWidget {
  const SpikeScreen({super.key, required this.bridge});

  final UnityBridge bridge;

  @override
  State<SpikeScreen> createState() => _SpikeScreenState();
}

class _SpikeScreenState extends State<SpikeScreen> {
  StreamSubscription<BridgeMessage>? _sub;
  String _status = 'waiting for unity.ready…';
  String _lastSwipe = '-';
  int _swipes = 0;
  String _rtt = '-';
  String _pause = '-';
  bool _failed = false;
  int _pingId = 0;
  final _pending = <int, Completer<void>>{};
  final _pauseAck = <String, Completer<void>>{};

  UnityBridge get bridge => widget.bridge;

  @override
  void initState() {
    super.initState();
    _sub = bridge.messages.listen(_onMessage);
    _waitReady();
  }

  Future<void> _waitReady() async {
    setState(() {
      _failed = false;
      _status = 'waiting for unity.ready…';
    });
    try {
      await bridge.waitUntilReady();
      if (!mounted) return;
      setState(() => _status = 'ready (unity ${bridge.readyPayload['buildVersion']})');
    } on TimeoutException {
      if (!mounted) return;
      setState(() {
        _failed = true;
        _status = 'Unity did not start';
      });
    }
  }

  void _onMessage(BridgeMessage m) {
    switch (m.type) {
      case 'spike.pong':
        final echo = m.payload['echo'];
        if (echo is Map && echo['id'] is int) _pending.remove(echo['id'])?.complete();
      case 'spike.swipe':
        setState(() {
          _swipes++;
          _lastSwipe = '${m.payload['dir']}';
        });
      case 'spike.paused':
      case 'spike.resumed':
        _pauseAck.remove(m.type)?.complete();
      default:
        break;
    }
  }

  Future<void> _runPings() async {
    const n = 20;
    final samples = <double>[];
    for (var i = 0; i < n; i++) {
      final id = ++_pingId;
      final done = _pending[id] = Completer<void>();
      final sw = Stopwatch()..start();
      bridge.send(BridgeMessage('spike.ping', payload: {'id': id}));
      try {
        await done.future.timeout(const Duration(seconds: 2));
      } on TimeoutException {
        _pending.remove(id);
        Log.write(Log.error, 'ping $id timed out');
        continue;
      }
      samples.add(sw.elapsedMicroseconds / 1000.0);
    }
    samples.sort();
    final text = samples.isEmpty
        ? 'all pings lost'
        : 'n=${samples.length}/$n median=${samples[samples.length ~/ 2].toStringAsFixed(1)}ms '
            'max=${samples.last.toStringAsFixed(1)}ms';
    Log.write(Log.perf, 'spike_rtt $text');
    setState(() => _rtt = text);
  }

  Future<void> _pauseResume() async {
    Future<bool> step(String send, String ack) async {
      final c = _pauseAck[ack] = Completer<void>();
      bridge.send(BridgeMessage(send));
      try {
        await c.future.timeout(const Duration(seconds: 2));
        return true;
      } on TimeoutException {
        return false;
      }
    }

    final paused = await step('game.pause', 'spike.paused');
    pauseUnity();
    await Future<void>.delayed(const Duration(milliseconds: 500));
    resumeUnity();
    final resumed = await step('game.resume', 'spike.resumed');
    final text = paused && resumed ? 'ok' : 'FAILED paused=$paused resumed=$resumed';
    Log.write(Log.game, 'spike_pause $text');
    setState(() => _pause = text);
  }

  @override
  void dispose() {
    _sub?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final ready = bridge.state == BridgeState.ready;
    return Scaffold(
      appBar: AppBar(title: const Text('Unity spike')),
      body: Column(
        children: [
          Expanded(child: EmbedUnity(onMessageFromUnity: bridge.transport.deliver)),
          Padding(
            padding: const EdgeInsets.all(12),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('status: $_status', key: const Key('spike_status')),
                Text('swipes: $_swipes last: $_lastSwipe', key: const Key('spike_swipe')),
                Text('rtt: $_rtt', key: const Key('spike_rtt')),
                Text('pause/resume: $_pause', key: const Key('spike_pause')),
                const SizedBox(height: 8),
                Wrap(spacing: 8, runSpacing: 8, children: [
                  if (_failed) FilledButton(onPressed: _waitReady, child: const Text('Retry')),
                  FilledButton(onPressed: ready ? _runPings : null, child: const Text('Ping x20')),
                  FilledButton(onPressed: ready ? _pauseResume : null, child: const Text('Pause+Resume')),
                  OutlinedButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Back')),
                ]),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
