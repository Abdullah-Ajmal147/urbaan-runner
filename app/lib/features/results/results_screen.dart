import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:urban_runner/core/widgets/phosphor.dart';
import 'package:provider/provider.dart';

import '../../core/ads/ads.dart';
import '../../core/app_services.dart';
import '../../core/theme/tokens.dart';
import '../../core/widgets/widgets.dart';
import '../../games/urban_runner/game_state.dart';
import '../home/hunt_card.dart';
import '../../games/urban_runner/items.dart';
import '../../games/urban_runner/missions.dart';
import '../game/game_screen.dart';

class ResultsScreen extends StatefulWidget {
  const ResultsScreen({super.key, required this.reward, this.rewardedThisRun = false});

  final RunReward reward;
  final bool rewardedThisRun;

  static Route<void> route(RunReward reward, {bool rewardedThisRun = false}) => PageRouteBuilder<void>(
        settings: const RouteSettings(name: '/results'),
        transitionDuration: UrMotion.slow,
        pageBuilder: (_, _, _) => ResultsScreen(reward: reward, rewardedThisRun: rewardedThisRun),
        transitionsBuilder: (_, a, _, child) => FadeTransition(opacity: a, child: child),
      );

  @override
  State<ResultsScreen> createState() => _ResultsScreenState();
}

class _ResultsScreenState extends State<ResultsScreen> with SingleTickerProviderStateMixin {
  late final AppServices s = context.read<AppServices>();
  late final GameState state = context.read<GameState>();
  late final AnimationController _confetti = AnimationController(vsync: this, duration: const Duration(seconds: 3));
  bool _doubled = false;
  bool _doubling = false;
  bool _watchedRewarded = false;

  RunReward get r => widget.reward;

  @override
  void initState() {
    super.initState();
    _watchedRewarded = widget.rewardedThisRun;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (r.newBest) {
        s.sounds.newBest();
        if (!MotionScope.reduced(context)) _celebrate();
      } else {
        s.sounds.results();
      }
    });
  }

  /// Two bursts of confetti, then stop (an endless loop would redraw Results forever).
  Future<void> _celebrate() async {
    try {
      for (var i = 0; i < 2 && mounted; i++) {
        await _confetti.forward(from: 0);
      }
    } on TickerCanceled {
      // left the screen mid-burst
    }
  }

  @override
  void dispose() {
    _confetti.dispose();
    super.dispose();
  }

  Future<void> _double() async {
    setState(() => _doubling = true);
    s.analytics.log('ad_rewarded_start', {'placement': 'double_reward'});
    final granted = await s.ads.showRewarded('double_reward');
    s.analytics.log(granted ? 'ad_rewarded_complete' : 'ad_rewarded_fail', {'placement': 'double_reward'});
    _watchedRewarded = true;
    if (granted && await state.grantDoubleReward(r)) {
      s.sounds.reward();
      if (mounted) setState(() => _doubled = true);
    }
    if (mounted) setState(() => _doubling = false);
  }

  /// Interstitials only when leaving Results, never after a rewarded ad (HANDOVER §7.4).
  Future<void> _maybeInterstitial() async {
    final p = state.profile;
    final policy = InterstitialPolicy(
      firstAfterRuns: s.config.integer('interstitial_first_after_runs'),
      everyNRuns: s.config.integer('interstitial_every_n_runs'),
      minIntervalS: s.config.integer('interstitial_min_interval_s'),
    );
    final show = policy.shouldShow(
      removeAds: p.removeAds,
      firstAppSession: p.appSessions <= 1,
      tutorialRun: r.result.mode == 'tutorial',
      justWatchedRewarded: _watchedRewarded,
      totalRuns: p.totalRuns,
      runsSinceLast: p.runsSinceInterstitial,
      lastShownMs: p.lastInterstitialAtMs,
      nowMs: DateTime.now().millisecondsSinceEpoch,
    );
    if (!show) return;
    s.bridge.transport.pause();
    final shown = await s.ads.showInterstitial();
    s.bridge.transport.resume();
    if (!shown) return; // nothing was loaded: don't start the cool-down for an ad nobody saw
    s.analytics.log('ad_interstitial_show', {'runs_since_last': p.runsSinceInterstitial});
    await state.markInterstitialShown();
  }

  Future<void> _again() async {
    s.sounds.tap();
    await _maybeInterstitial();
    if (!mounted) return;
    Navigator.of(context).pushReplacement(GameScreen.route(kind: r.result.isDaily ? RunKind.daily : RunKind.normal));
  }

  Future<void> _home() async {
    s.sounds.tap();
    await _maybeInterstitial();
    if (!mounted) return;
    Navigator.of(context).popUntil((route) => route.isFirst);
  }

  String get _headline {
    final res = r.result;
    if (res.mode == 'tutorial') return "You're ready";
    if (r.newBest) return 'New personal best';
    if (r.dailyBest) return "Today's best Daily Run";
    return res.cause == 'quit' ? 'Run ended' : 'Nice run';
  }

  @override
  Widget build(BuildContext context) {
    final res = r.result;
    final distance = res.distanceM.floor();
    final best = r.newBest;
    final header = best ? UrColors.success : UrColors.plum;
    final ink = best ? UrColors.successInk : UrColors.text;
    final levelP = state.xpForNextLevel == 0 ? 0.0 : state.xpIntoLevel / state.xpForNextLevel;
    final gainedP = state.xpForNextLevel == 0 ? 0.0 : math.min(levelP, r.xpGained / state.xpForNextLevel);
    final nextUnlock = Items.all.where((i) => i.unlock == UnlockKind.level && !state.isOwned(i.id)).fold<LockerItem?>(null, (a, b) => a == null || b.level < a.level ? b : a);
    final reduced = MotionScope.reduced(context);
    final canDouble = s.config.flag('double_reward_enabled') && r.sparksCredited > 0 && !r.alreadyProcessed;

    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) _home();
      },
      child: Scaffold(
        body: Stack(children: [
          ListView(padding: EdgeInsets.zero, children: [
            Container(
              height: 330,
              color: header,
              child: Stack(clipBehavior: Clip.hardEdge, children: [
                if (best && !reduced) Positioned.fill(child: _Confetti(controller: _confetti)),
                Positioned(right: -70, top: 50, width: 260, child: Image.asset('assets/images/${state.profile.selectedCharacter}.png', semanticLabel: 'Your courier')),
                Positioned(left: 24, top: MediaQuery.paddingOf(context).top + 28, right: 150, child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Row(children: [
                    Icon(best ? PhosphorIcons.trophy(PhosphorIconsStyle.fill) : PhosphorIcons.flagCheckered(PhosphorIconsStyle.fill), size: 18, color: ink),
                    const SizedBox(width: 6),
                    Flexible(child: Text(_headline, style: UrText.label.copyWith(color: ink, fontWeight: FontWeight.w700))),
                  ]),
                  const SizedBox(height: 6),
                  FittedBox(
                    fit: BoxFit.scaleDown,
                    alignment: Alignment.centerLeft,
                    child: CountUp(value: distance, duration: const Duration(milliseconds: 1100), style: UrText.hero.copyWith(fontSize: 92, height: .9, letterSpacing: -4, color: ink)),
                  ),
                  Text('metres', style: UrText.cardTitle.copyWith(fontSize: 22, color: ink)),
                  const SizedBox(height: 10),
                  Text(
                    best && r.previousBest > 0
                        ? '${distance - r.previousBest} m further than your old best.'
                        : res.mode == 'tutorial'
                            // Lessons never count as a best (negative test: the screen claimed one anyway).
                            ? 'Lesson done · ${_duration(res.durationS)}'
                            : 'Best ${CountUp.format(math.max(r.previousBest, distance))} m · ${_duration(res.durationS)}',
                    style: UrText.label.copyWith(color: ink.withValues(alpha: .8)),
                  ),
                ])),
              ]),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 0),
              child: Panel(
                padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 6),
                child: Column(children: [
                  _ReceiptRow(icon: PhosphorIcons.sparkle(PhosphorIconsStyle.fill), color: UrColors.cyan, label: _doubled ? 'Sparks collected ×2' : 'Sparks collected', value: r.sparksCredited * (_doubled ? 2 : 1)),
                  for (final id in r.missionsCompleted)
                    _ReceiptRow(icon: PhosphorIcons.checkCircle(PhosphorIconsStyle.fill), color: UrColors.success, label: 'Mission ready · ${MissionPool.byId(id).title}', value: null, trailing: 'Claim in Missions'),
                  if (r.huntFoundNow)
                    Padding(
                      padding: const EdgeInsets.symmetric(vertical: 12),
                      child: Row(children: [
                        Expanded(
                          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                            Text(r.huntPrize > 0 ? 'Lantern Hunt complete!' : 'Lantern found · ${r.huntFoundToday} of 5', style: UrText.label.copyWith(color: UrColors.amber)),
                            const SizedBox(height: 6),
                            HuntLanterns(found: r.huntFoundToday, size: 18, justFound: r.huntFoundToday - 1),
                          ]),
                        ),
                        if (r.huntPrize > 0) SparksAmount(value: r.huntPrize, size: 18),
                      ]),
                    ),
                  if (r.levelAfter > r.levelBefore)
                    _ReceiptRow(icon: PhosphorIcons.arrowFatUp(PhosphorIconsStyle.fill), color: UrColors.amber, label: 'Level ${r.levelAfter} reached', value: r.levelUpSparks),
                  for (final id in r.unlocked)
                    _ReceiptRow(icon: PhosphorIcons.lockOpen(PhosphorIconsStyle.fill), color: UrColors.magenta, label: 'Unlocked · ${Items.byId(id).name}', value: null),
                  Padding(
                    padding: const EdgeInsets.symmetric(vertical: 14),
                    child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                      Row(children: [
                        Text('Level ${state.level} → ${state.level + 1}', style: UrText.label.copyWith(color: UrColors.muted)),
                        const Spacer(),
                        Text('+${r.xpGained} XP', style: UrText.label.copyWith(color: UrColors.success)),
                      ]),
                      const SizedBox(height: 8),
                      ProgressBar(value: levelP - gainedP, extra: gainedP, color: UrColors.success, extraColor: const Color(0xFFA6FFD3), height: 10),
                      if (nextUnlock != null) ...[
                        const SizedBox(height: 8),
                        Text.rich(TextSpan(style: UrText.small, children: [
                          const TextSpan(text: 'Next unlock: '),
                          TextSpan(text: '${nextUnlock.name} at level ${nextUnlock.level}', style: const TextStyle(color: UrColors.amber, fontWeight: FontWeight.w700)),
                        ])),
                      ],
                    ]),
                  ),
                ]),
              ),
            ),
            if (canDouble)
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 14, 16, 0),
                child: Semantics(
                  button: true,
                  label: _doubled ? 'Sparks doubled' : 'Double your sparks by watching an ad',
                  child: OutlinedButton.icon(
                    onPressed: _doubled || _doubling ? null : _double,
                    style: OutlinedButton.styleFrom(
                      minimumSize: const Size.fromHeight(58),
                      foregroundColor: UrColors.amber,
                      disabledForegroundColor: UrColors.success,
                      side: BorderSide(color: _doubled ? UrColors.success : UrColors.amber, width: 1.5),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(UrRadius.button)),
                      textStyle: UrText.label.copyWith(fontSize: 15),
                    ),
                    icon: Icon(_doubled ? PhosphorIcons.check(PhosphorIconsStyle.bold) : PhosphorIcons.play(PhosphorIconsStyle.fill), size: 18),
                    label: Text(_doubled ? 'Sparks doubled' : _doubling ? 'Loading ad…' : 'Double your sparks · watch an ad'),
                  ),
                ),
              ),
            const SizedBox(height: 120),
          ]),
          Positioned(
            left: 16,
            right: 16,
            bottom: 16 + MediaQuery.paddingOf(context).bottom,
            child: Row(children: [
              Semantics(
                button: true,
                label: 'Home',
                child: Material(
                  color: UrColors.surface,
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20), side: const BorderSide(color: UrColors.hairline)),
                  child: InkWell(
                    borderRadius: BorderRadius.circular(20),
                    onTap: _home,
                    child: SizedBox(width: 64, height: 64, child: Icon(PhosphorIcons.house(PhosphorIconsStyle.bold), color: UrColors.text)),
                  ),
                ),
              ),
              const SizedBox(width: 10),
              Expanded(child: PressButton(label: 'Run again', icon: PhosphorIcons.arrowClockwise(PhosphorIconsStyle.bold), onPressed: _again)),
            ]),
          ),
        ]),
      ),
    );
  }
}

class _ReceiptRow extends StatelessWidget {
  const _ReceiptRow({required this.icon, required this.color, required this.label, required this.value, this.trailing});
  final IconData icon;
  final Color color;
  final String label;
  final int? value;
  final String? trailing;

  @override
  Widget build(BuildContext context) => Container(
        constraints: const BoxConstraints(minHeight: 56),
        decoration: const BoxDecoration(border: Border(bottom: BorderSide(color: UrColors.hairline))),
        child: Row(children: [
          Icon(icon, color: color, size: 18),
          const SizedBox(width: 10),
          Expanded(child: Text(label, style: UrText.label)),
          if (value != null) ...[const Text('+', style: UrText.number), CountUp(value: value!, style: UrText.number)],
          if (trailing != null) Text(trailing!, style: UrText.small),
        ]),
      );
}

/// Lightweight confetti for a new best (palette colours, no packages).
class _Confetti extends StatelessWidget {
  const _Confetti({required this.controller});
  final AnimationController controller;

  @override
  Widget build(BuildContext context) => AnimatedBuilder(
        animation: controller,
        builder: (_, _) => CustomPaint(painter: _ConfettiPainter(controller.value)),
      );
}

class _ConfettiPainter extends CustomPainter {
  _ConfettiPainter(this.t);
  final double t;
  static const _colors = [UrColors.night, UrColors.magenta, UrColors.amber, UrColors.cyan];

  @override
  void paint(Canvas canvas, Size size) {
    final p = Paint();
    for (var i = 0; i < 26; i++) {
      final phase = (t + i * 0.137) % 1.0;
      final x = ((i * 97) % 100) / 100 * size.width + math.sin((phase + i) * 6) * 12;
      final y = phase * (size.height + 40) - 20;
      p.color = _colors[i % _colors.length];
      canvas.save();
      canvas.translate(x, y);
      canvas.rotate(phase * 9 + i);
      canvas.drawRRect(RRect.fromRectAndRadius(const Rect.fromLTWH(-3, -6, 7, 12), const Radius.circular(2)), p);
      canvas.restore();
    }
  }

  @override
  bool shouldRepaint(_ConfettiPainter old) => old.t != t;
}

/// "28 s", "1 min 05 s": never "0 min 28 s".
String _duration(double seconds) {
  final total = seconds.isFinite && seconds > 0 ? seconds.round() : 0;
  final m = total ~/ 60, sec = total % 60;
  return m == 0 ? '$sec s' : '$m min ${sec.toString().padLeft(2, '0')} s';
}
