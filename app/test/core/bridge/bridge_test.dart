import 'dart:async';
import 'dart:convert';

import 'package:fake_async/fake_async.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:urban_runner/core/bridge/bridge_message.dart';
import 'package:urban_runner/core/bridge/unity_bridge.dart';
import 'package:urban_runner/core/log.dart';

import '../../support/fake_transport.dart';

void main() {
  final logs = <String>[];
  setUp(() {
    logs.clear();
    Log.sink = (tag, msg) => logs.add('$tag $msg');
  });

  group('BridgeMessage', () {
    test('round-trips', () {
      final m = BridgeMessage('session.start', sessionId: 'abc', payload: {'seed': 42, 'mode': 'daily'});
      final back = BridgeMessage.decode(m.encode());
      expect(back.v, kBridgeVersion);
      expect(back.type, 'session.start');
      expect(back.sessionId, 'abc');
      expect(back.payload, {'seed': 42, 'mode': 'daily'});
    });

    test('omits sessionId when null and defaults payload', () {
      final json = jsonDecode(BridgeMessage('game.pause').encode()) as Map;
      expect(json.containsKey('sessionId'), isFalse);
      expect(BridgeMessage.decode('{"v":2,"type":"game.pause"}').payload, isEmpty);
    });

    test('writes decimals with a dot', () {
      expect(BridgeMessage('x', payload: {'d': 1.5}).encode(), contains('1.5'));
    });

    for (final bad in ['', 'nope', '[]', '{"type":"x"}', '{"v":"1","type":"x"}', '{"v":2}', '{"v":2,"type":"x","payload":[1]}']) {
      test('rejects malformed: $bad', () {
        expect(() => BridgeMessage.decode(bad), throwsA(isA<BridgeFormatException>()));
      });
    }
  });

  group('UnityBridge', () {
    late FakeTransport t;
    late UnityBridge bridge;

    setUp(() {
      t = FakeTransport();
      bridge = UnityBridge(t);
    });

    const ready = '{"v":3,"type":"unity.ready","payload":{"bridgeVersion":3,"buildVersion":"0.1.0"}}';

    test('refuses to send before unity.ready', () {
      expect(() => bridge.send(BridgeMessage('game.pause')), throwsStateError);
      expect(t.sent, isEmpty);
    });

    test('becomes ready on unity.ready and then sends', () async {
      final wait = bridge.waitUntilReady();
      t.deliver(ready);
      await wait;
      expect(bridge.state, BridgeState.ready);
      expect(bridge.readyPayload['buildVersion'], '0.1.0');
      bridge.send(BridgeMessage('game.pause'));
      expect(jsonDecode(t.sent.single)['type'], 'game.pause');
    });

    test('ready that arrives before waiting is remembered', () async {
      t.deliver(ready);
      await bridge.waitUntilReady();
      expect(bridge.state, BridgeState.ready);
    });

    test('times out after 10 s, and retry can still succeed', () {
      fakeAsync((async) {
        Object? error;
        bridge.waitUntilReady().catchError((Object e) => error = e);
        async.elapse(const Duration(seconds: 9));
        expect(error, isNull);
        async.elapse(const Duration(seconds: 2));
        expect(error, isA<TimeoutException>());
        expect(bridge.state, BridgeState.timedOut);

        var done = false;
        bridge.waitUntilReady().then((_) => done = true);
        t.deliver(ready);
        async.flushMicrotasks();
        expect(done, isTrue);
      });
    });

    test('drops malformed and wrong-version messages with UR_ERROR', () async {
      final got = <BridgeMessage>[];
      bridge.messages.listen(got.add);
      t.deliver('{garbage');
      t.deliver('{"v":99,"type":"session.ended"}');
      await Future<void>.delayed(Duration.zero);
      expect(got, isEmpty);
      expect(logs.where((l) => l.startsWith(Log.error)), hasLength(2));
    });

    test('passes unknown types through for callers to ignore', () async {
      final got = <BridgeMessage>[];
      bridge.messages.listen(got.add);
      t.deliver('{"v":3,"type":"something.new"}');
      await Future<void>.delayed(Duration.zero);
      expect(got.single.type, 'something.new');
    });
  });
}
