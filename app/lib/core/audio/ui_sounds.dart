import 'package:audioplayers/audioplayers.dart';
import 'package:flutter/services.dart';

/// Menu sounds and haptics (HANDOVER §3.10): light haptic on UI taps, success pattern on a new best.
/// Respects the player's SFX and haptics settings.
class UiSounds {
  UiSounds({this.enabled = true});

  bool enabled;
  bool sfx = true;
  bool haptics = true;
  final _pool = <String, AudioPlayer>{};

  Future<void> _play(String asset, double volume) async {
    if (!enabled || !sfx) return;
    try {
      final p = _pool.putIfAbsent(asset, () => AudioPlayer()..setPlayerMode(PlayerMode.lowLatency));
      await p.stop();
      await p.play(AssetSource('audio/$asset'), volume: volume);
    } catch (_) {
      // Audio is a nicety; never break a tap over it.
    }
  }

  void tap() {
    if (haptics && enabled) HapticFeedback.lightImpact();
    _play('ui_tap.ogg', 0.6);
  }

  void reward() {
    if (haptics && enabled) HapticFeedback.mediumImpact();
    _play('reward.ogg', 0.8);
  }

  Future<void> newBest() async {
    _play('new_best.ogg', 0.9);
    if (!haptics || !enabled) return;
    for (var i = 0; i < 3; i++) {
      await HapticFeedback.heavyImpact();
      await Future<void>.delayed(const Duration(milliseconds: 90));
    }
  }

  void results() => _play('results.ogg', 0.7);
}
