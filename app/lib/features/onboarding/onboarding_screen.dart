import 'dart:async';

import 'package:flutter/material.dart';
import 'package:urban_runner/core/widgets/phosphor.dart';
import 'package:provider/provider.dart';

import '../../core/app_services.dart';
import '../../core/theme/tokens.dart';
import '../../core/widgets/widgets.dart';
import '../../games/urban_runner/game_state.dart';

/// First launch: three swipe cards (controls, missions, locker), then ads consent (HANDOVER §4.2, §7.4).
class OnboardingScreen extends StatefulWidget {
  const OnboardingScreen({super.key, required this.onDone});
  final VoidCallback onDone;

  @override
  State<OnboardingScreen> createState() => _OnboardingScreenState();
}

class _OnboardingScreenState extends State<OnboardingScreen> {
  final _pages = PageController();
  int _page = 0;
  bool _finishing = false;
  static const _consentWait = Duration(seconds: 3);

  static const _cards = [
    _Card(
      color: UrColors.magenta,
      ink: UrColors.magentaInk,
      image: 'assets/images/mira_run.png',
      title: 'Swipe to run',
      body: 'Left and right to change lanes. Up to jump crates, down to slide under signs. Your first run teaches you.',
      icon: 'swipe',
    ),
    _Card(
      color: UrColors.success,
      ink: UrColors.successInk,
      image: 'assets/images/dex.png',
      title: 'Missions pay out',
      body: 'Three missions at a time. Finish one for sparks and XP, and a new one rolls in.',
      icon: 'target',
    ),
    _Card(
      color: UrColors.amber,
      ink: UrColors.amberInk,
      image: 'assets/images/juno.png',
      title: 'Build your crew',
      body: 'Unlock couriers and trails, and upgrade your power-ups with the sparks you collect.',
      icon: 'shirt',
    ),
  ];

  Future<void> _next() async {
    context.read<AppServices>().sounds.tap();
    if (_page < _cards.length - 1) {
      await _pages.nextPage(duration: UrMotion.slow, curve: Curves.easeOutCubic);
      return;
    }
    setState(() => _finishing = true);
    final s = context.read<AppServices>();
    // Google's consent flow runs before any ad is requested (HANDOVER §7.4); init() waits for it too.
    // A slow consent check must not keep a new player on "One moment…" (D37), so cap the wait here.
    await s.ads.gatherConsent().timeout(_consentWait, onTimeout: () {});
    unawaited(s.ads.init());
    if (!mounted) return;
    await context.read<GameState>().completeOnboarding();
    widget.onDone();
  }

  @override
  void dispose() {
    _pages.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final card = _cards[_page];
    return Scaffold(
      body: AnimatedContainer(
        duration: UrMotion.normal,
        color: card.color,
        child: SafeArea(
          child: Column(children: [
            Expanded(
              child: PageView.builder(
                controller: _pages,
                itemCount: _cards.length,
                onPageChanged: (i) => setState(() => _page = i),
                itemBuilder: (_, i) => _cards[i],
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 0, 20, 20),
              child: Column(children: [
                Semantics(
                  label: 'Page ${_page + 1} of ${_cards.length}',
                  excludeSemantics: true,
                  child: Row(mainAxisAlignment: MainAxisAlignment.center, children: [
                    for (var i = 0; i < _cards.length; i++)
                      AnimatedContainer(
                        duration: UrMotion.fast,
                        margin: const EdgeInsets.symmetric(horizontal: 4),
                        width: i == _page ? 22 : 8,
                        height: 8,
                        decoration: BoxDecoration(color: card.ink.withValues(alpha: i == _page ? 1 : .3), borderRadius: BorderRadius.circular(4)),
                      ),
                  ]),
                ),
                const SizedBox(height: 18),
                PressButton(
                  key: const Key('onboarding_next'),
                  label: _page < _cards.length - 1 ? 'Next' : _finishing ? 'One moment…' : "Let's go",
                  color: card.ink,
                  shade: Colors.black,
                  ink: card.color,
                  onPressed: _finishing ? null : _next,
                ),
              ]),
            ),
          ]),
        ),
      ),
    );
  }
}

class _Card extends StatelessWidget {
  const _Card({required this.color, required this.ink, required this.image, required this.title, required this.body, required this.icon});
  final Color color, ink;
  final String image, title, body, icon;

  IconData get _icon => switch (icon) {
        'swipe' => PhosphorIcons.handSwipeRight(PhosphorIconsStyle.fill),
        'target' => PhosphorIcons.target(PhosphorIconsStyle.fill),
        _ => PhosphorIcons.tShirt(PhosphorIconsStyle.fill),
      };

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(horizontal: 24),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          const SizedBox(height: 12),
          Icon(_icon, color: ink, size: 34),
          Expanded(child: Center(child: Image.asset(image, fit: BoxFit.contain, excludeFromSemantics: true))),
          Text(title, style: UrText.hero.copyWith(fontSize: 40, color: ink, letterSpacing: -1.5)),
          const SizedBox(height: 10),
          Text(body, style: UrText.body.copyWith(fontSize: 16, color: ink.withValues(alpha: .8))),
          const SizedBox(height: 24),
        ]),
      );
}
