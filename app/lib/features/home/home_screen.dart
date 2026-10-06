import 'package:flutter/material.dart';
import 'package:urban_runner/core/widgets/phosphor.dart';
import 'package:provider/provider.dart';

import '../../core/app_services.dart';
import '../../core/theme/tokens.dart';
import '../../core/widgets/widgets.dart';
import '../../games/urban_runner/game_state.dart';
import '../../games/urban_runner/items.dart';
import '../../games/urban_runner/profile.dart';
import '../game/game_screen.dart';
import '../profile/profile_screen.dart';
import '../settings/settings_screen.dart';

class HomeScreen extends StatelessWidget {
  const HomeScreen({super.key, required this.onOpenMissions});
  final VoidCallback onOpenMissions;

  @override
  Widget build(BuildContext context) {
    final state = context.watch<GameState>();
    final s = context.read<AppServices>();
    final p = state.profile;
    final levelP = state.xpForNextLevel == 0 ? 0.0 : state.xpIntoLevel / state.xpForNextLevel;
    final ready = p.missions.where((m) => m.claimable).length;
    final dailyLeft = DailyRun.timeLeft(DateTime.now().toUtc());
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
            Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              const Text('Courier level', style: UrText.small),
              SparksAmount(value: p.sparks),
            ]),
            const Spacer(),
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
          Positioned(left: 0, right: 0, bottom: 0, height: 110, child: Container(color: UrColors.plumDeep)),
          Positioned(left: -60, right: -60, top: 36, child: _Float(enabled: !reduced, child: Image.asset('assets/images/crew.png', semanticLabel: 'Juno, Mira and Dex, the three couriers, running together'))),
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
    if (widget.enabled) _c.repeat(reverse: true);
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
