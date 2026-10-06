import 'dart:async';

import 'package:flutter/material.dart';
import 'package:urban_runner/core/widgets/phosphor.dart';
import 'package:provider/provider.dart';

import '../../core/app_services.dart';
import '../../core/theme/tokens.dart';
import '../../core/widgets/widgets.dart';
import '../../games/urban_runner/game_state.dart';
import 'hunt_card.dart';
import 'streak_sheet.dart';
import '../../games/urban_runner/items.dart';
import '../../games/urban_runner/profile.dart';
import '../game/game_screen.dart';
import '../profile/profile_screen.dart';
import '../settings/settings_screen.dart';

class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key, required this.onOpenMissions});
  final VoidCallback onOpenMissions;

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

/// Rebuilds when the app returns to the foreground and once a minute, so the Daily Run countdown and the
/// day itself never go stale while Home stays open (negative test N18 found "11h left" a day later).
class _HomeScreenState extends State<HomeScreen> with WidgetsBindingObserver {
  Timer? _minute;

  VoidCallback get onOpenMissions => widget.onOpenMissions;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _minute = Timer.periodic(const Duration(minutes: 1), (_) => mounted ? setState(() {}) : null);
    // Today's streak gift, once per app session, when Home first appears (D32).
    if (!StreakSheet.offeredThisSession) {
      StreakSheet.offeredThisSession = true;
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) StreakSheet.maybeShow(context);
      });
    }
  }



  @override
  void didChangeAppLifecycleState(AppLifecycleState s) {
    if (s == AppLifecycleState.resumed && mounted) setState(() {});
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _minute?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<GameState>();
    final s = context.read<AppServices>();
    final p = state.profile;
    final levelP = state.xpForNextLevel == 0 ? 0.0 : state.xpIntoLevel / state.xpForNextLevel;
    final ready = p.missions.where((m) => m.claimable).length;
    final dailyLeft = DailyRun.timeLeft(state.now().toUtc());
    final todayBest = state.todayDailyBest;
    final firstRun = !p.tutorialDone;

    void play(RunKind kind) {
      s.sounds.tap();
      Navigator.of(context).push(GameScreen.route(kind: kind));
    }

    return Stack(children: [
      SafeArea(
        bottom: false,
        child: ListView(padding: const EdgeInsets.fromLTRB(16, 8, 16, 24), children: [
          Row(children: [
            LevelRing(level: state.level, progress: levelP),
            const SizedBox(width: 10),
            Expanded(
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                const Text('Courier level', style: UrText.small, overflow: TextOverflow.ellipsis),
                FittedBox(fit: BoxFit.scaleDown, alignment: Alignment.centerLeft, child: SparksAmount(value: p.sparks)),
              ]),
            ),
            if (p.streak > 0 && !firstRun) ...[
              _StreakChip(days: state.streakStatus.claimedToday ? p.streak : 0, onTap: () => StreakSheet.maybeShowAlways(context)),
              const SizedBox(width: 8),
            ],
            IconSquareButton(
              icon: PhosphorIcons.user(PhosphorIconsStyle.bold),
              label: 'Profile',
              onTap: () {
                s.sounds.tap();
                Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => const ProfileScreen(), settings: const RouteSettings(name: '/profile')));
              },
            ),
            const SizedBox(width: 8),
            IconSquareButton(
              icon: PhosphorIcons.gearSix(PhosphorIconsStyle.bold),
              label: 'Settings',
              onTap: () {
                s.sounds.tap();
                Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => const SettingsScreen(), settings: const RouteSettings(name: '/settings')));
              },
            ),
          ]),
          const SizedBox(height: 14),
          _HeroCard(firstRun: firstRun, juno: state.isOwned(Items.juno.id)),
          const SizedBox(height: 16),
          PressButton(
            key: const Key('play'),
            height: 86,
            label: firstRun ? 'Start your first run' : 'Play',
            subtitle: firstRun ? 'A quick lesson, then the street is yours' : p.bestDistance > 0 ? 'Best ${CountUp.format(p.bestDistance)} m · beat it tonight' : 'The market is open',
            icon: PhosphorIcons.play(PhosphorIconsStyle.fill),
            onPressed: () => play(RunKind.normal),
          ),
          const SizedBox(height: 18),
          Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Expanded(
              child: Panel(
                key: const Key('daily'),
                semanticsLabel: 'Daily Run. ${todayBest > 0 ? 'Your best today is $todayBest metres.' : 'Same track for everyone today.'}',
                onTap: firstRun ? null : () => play(RunKind.daily),
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Row(children: [
                    Icon(PhosphorIcons.calendarDots(PhosphorIconsStyle.fill), size: 20, color: UrColors.amber),
                    const Spacer(),
                    Text('${dailyLeft.inHours}h left', style: UrText.small.copyWith(color: UrColors.amber, fontWeight: FontWeight.w700)),
                  ]),
                  const SizedBox(height: 10),
                  const Text('Daily Run', style: UrText.cardTitle),
                  const SizedBox(height: 2),
                  Text(todayBest > 0 ? 'Today ${CountUp.format(todayBest)} m' : 'Same track for everyone', style: UrText.small),
                ]),
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Panel(
                semanticsLabel: 'Missions. $ready of ${p.missions.length} ready to claim.',
                onTap: onOpenMissions,
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Row(children: [
                    Icon(PhosphorIcons.target(PhosphorIconsStyle.fill), size: 20, color: UrColors.success),
                    const Spacer(),
                    Text(ready > 0 ? '$ready to claim' : '${p.missions.length} active', style: UrText.small.copyWith(color: UrColors.success, fontWeight: FontWeight.w700)),
                  ]),
                  const SizedBox(height: 10),
                  const Text('Missions', style: UrText.cardTitle),
                  const SizedBox(height: 10),
                  Row(children: [
                    for (var i = 0; i < p.missions.length; i++) ...[
                      if (i > 0) const SizedBox(width: 4),
                      Expanded(child: ProgressBar(value: p.missions[i].fraction, height: 5)),
                    ],
                  ]),
                ]),
              ),
            ),
          ]),
          if (!firstRun) ...[
            const SizedBox(height: 10),
            const HuntCard(),
          ],
          const SizedBox(height: 10),
          Panel(
            color: UrColors.night,
            child: Row(children: [
              Icon(PhosphorIcons.sparkle(PhosphorIconsStyle.bold), color: UrColors.subtle, size: 20),
              const SizedBox(width: 10),
              const Expanded(child: Text('More games are coming to the factory. Your progress here will keep counting.', style: UrText.small)),
            ]),
          ),
        ]),
      ),
      const Positioned.fill(child: Grain()),
    ]);
  }
}

class _HeroCard extends StatelessWidget {
  const _HeroCard({required this.firstRun, required this.juno});
  final bool firstRun;
  final bool juno;

  @override
  Widget build(BuildContext context) {
    final reduced = MotionScope.reduced(context);
    return ClipRRect(
      borderRadius: BorderRadius.circular(UrRadius.hero),
      child: Container(
        height: 340,
        color: UrColors.plum,
        child: Stack(children: [
          Positioned(
            left: -6,
            top: 4,
            child: Text('NIGHT\nSHIFT', style: UrText.hero.copyWith(
              fontSize: 104,
              height: .86,
              letterSpacing: -4,
              foreground: Paint()
                ..style = PaintingStyle.stroke
                ..strokeWidth = 1.5
                ..color = UrColors.magenta.withValues(alpha: .35),
            )),
          ),
          Positioned(right: -30, top: -40, child: Container(width: 190, height: 190, decoration: BoxDecoration(shape: BoxShape.circle, color: UrColors.amber.withValues(alpha: .16)))),
          Positioned(left: 0, right: 0, bottom: 0, height: 104, child: Container(color: UrColors.plumDeep)),
          // The crew stands above the text band, never over the title (negative test N15 found the overlap).
          Positioned(
            left: -20,
            right: -20,
            top: 30,
            bottom: 104,
            child: _Float(
              enabled: !reduced,
              child: Image.asset('assets/images/crew.png', fit: BoxFit.contain, alignment: Alignment.bottomCenter, semanticLabel: 'Juno, Mira and Dex, the three couriers, running together'),
            ),
          ),
          Positioned(
            left: 20,
            right: 20,
            bottom: 18,
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              const Text('Urban Runner', style: UrText.hero),
              const SizedBox(height: 6),
              Text(firstRun ? 'New courier? The first run teaches you the moves.' : 'Parcels to deliver. The market closes at dawn.', style: UrText.body.copyWith(fontSize: 13)),
            ]),
          ),
          Positioned(right: 16, top: 16, child: Sticker(text: juno ? 'Juno is in your crew' : 'Juno joins at Lv 10')),
        ]),
      ),
    );
  }
}

class _Float extends StatefulWidget {
  const _Float({required this.child, required this.enabled});
  final Widget child;
  final bool enabled;
  @override
  State<_Float> createState() => _FloatState();
}

class _FloatState extends State<_Float> with SingleTickerProviderStateMixin {
  late final AnimationController _c = AnimationController(vsync: this, duration: const Duration(milliseconds: 3600));

  @override
  void initState() {
    super.initState();
    // A few gentle floats when Home appears, then rest: an endless loop redraws the screen 60×/s for as long as
    // the player sits on Home (battery), and keeps the UI from ever going idle.
    if (widget.enabled) _float();
  }

  Future<void> _float() async {
    try {
      for (var i = 0; i < 3 && mounted; i++) {
        await _c.forward();
        await _c.reverse();
      }
    } on TickerCanceled {
      // disposed mid-float
    }
  }

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => AnimatedBuilder(
        animation: _c,
        builder: (_, child) => Transform.translate(offset: Offset(0, -5 * Curves.easeInOut.transform(_c.value)), child: child),
        child: widget.child,
      );
}

/// "🔥 5" in the header: the streak at a glance; 0 means today's gift is still waiting.
class _StreakChip extends StatelessWidget {
  const _StreakChip({required this.days, required this.onTap});
  final int days;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) => Semantics(
        button: true,
        label: days > 0 ? 'Daily streak, $days days' : 'Daily gift waiting',
        excludeSemantics: true,
        child: GestureDetector(
          onTap: onTap,
          child: Container(
            height: 48,
            padding: const EdgeInsets.symmetric(horizontal: 12),
            decoration: BoxDecoration(
              color: days > 0 ? UrColors.surface : UrColors.amber,
              borderRadius: BorderRadius.circular(14),
              border: Border.all(color: UrColors.hairline),
            ),
            child: Row(mainAxisSize: MainAxisSize.min, children: [
              Icon(days > 0 ? PhosphorIcons.flame(PhosphorIconsStyle.fill) : PhosphorIcons.gift(PhosphorIconsStyle.fill),
                  color: days > 0 ? UrColors.amber : UrColors.amberInk, size: 20),
              if (days > 0) ...[const SizedBox(width: 6), Text('$days', style: UrText.number.copyWith(fontSize: 17))],
            ]),
          ),
        ),
      );
}
