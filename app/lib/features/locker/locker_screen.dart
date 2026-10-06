import 'package:flutter/material.dart';
import 'package:urban_runner/core/widgets/phosphor.dart';
import 'package:provider/provider.dart';

import '../../core/app_services.dart';
import '../../core/theme/tokens.dart';
import '../../core/widgets/widgets.dart';
import '../../games/urban_runner/game_state.dart';
import '../../games/urban_runner/items.dart';

/// Locker (approved design): the selected courier fills the top in their colour; tabs for couriers, trails, upgrades.
class LockerScreen extends StatefulWidget {
  const LockerScreen({super.key});
  @override
  State<LockerScreen> createState() => _LockerScreenState();
}

class _LockerScreenState extends State<LockerScreen> {
  int _tab = 0;
  String? _picked;

  @override
  Widget build(BuildContext context) {
    final state = context.watch<GameState>();
    final p = state.profile;
    final picked = Items.byId(_picked ?? p.selectedCharacter);
    final accent = Color(picked.accent);
    final ink = Color(picked.ink);
    return Column(children: [
      AnimatedContainer(
        duration: UrMotion.normal,
        color: accent,
        height: 360 + MediaQuery.paddingOf(context).top,
        child: Stack(clipBehavior: Clip.hardEdge, children: [
          Positioned(
            left: -8,
            bottom: -16,
            child: Text(picked.name.toUpperCase(), style: UrText.hero.copyWith(fontSize: 150, height: .8, letterSpacing: -8, color: ink.withValues(alpha: .16))),
          ),
          Positioned(
            left: 0,
            right: 0,
            top: MediaQuery.paddingOf(context).top + 40,
            height: 300,
            child: AnimatedSwitcher(
              duration: UrMotion.normal,
              child: Image.asset(picked.image!, key: ValueKey(picked.id), fit: BoxFit.contain, semanticLabel: '${picked.name}, full body'),
            ),
          ),
          Positioned(
            left: 20,
            right: 20,
            top: MediaQuery.paddingOf(context).top + 12,
            child: Row(children: [
              Text('Locker', style: UrText.cardTitle.copyWith(fontSize: 22, color: ink)),
              const Spacer(),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 7),
                decoration: BoxDecoration(color: UrColors.night.withValues(alpha: .88), borderRadius: BorderRadius.circular(12)),
                child: SparksAmount(value: p.sparks, size: 15),
              ),
            ]),
          ),
          Positioned(
            left: 20,
            bottom: 18,
            right: 20,
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(picked.name, style: UrText.hero.copyWith(fontSize: 40, color: ink)),
              Text(picked.tagline, style: UrText.label.copyWith(color: ink.withValues(alpha: .8))),
            ]),
          ),
        ]),
      ),
      Container(
        decoration: const BoxDecoration(border: Border(bottom: BorderSide(color: UrColors.hairline))),
        padding: const EdgeInsets.symmetric(horizontal: 16),
        child: Row(children: [
          for (final (i, label) in const [(0, 'Couriers'), (1, 'Trails'), (2, 'Upgrades')])
            Padding(
              padding: const EdgeInsets.only(right: 20),
              child: Semantics(
                selected: _tab == i,
                button: true,
                child: InkWell(
                  onTap: () => setState(() => _tab = i),
                  child: Container(
                    height: 48,
                    alignment: Alignment.center,
                    decoration: BoxDecoration(border: Border(bottom: BorderSide(width: 2, color: _tab == i ? UrColors.magenta : Colors.transparent))),
                    child: Text(label, style: UrText.label.copyWith(fontSize: 15, color: _tab == i ? UrColors.text : UrColors.faint, fontWeight: FontWeight.w700)),
                  ),
                ),
              ),
            ),
        ]),
      ),
      Expanded(
        child: ListView(padding: const EdgeInsets.all(16), children: [
          if (_tab == 0) _couriers(state, picked),
          if (_tab == 1) ...Items.trails.map((t) => _trail(state, t)),
          if (_tab == 2) ...PowerUp.values.map((u) => _upgrade(state, u)),
        ]),
      ),
    ]);
  }

  Widget _couriers(GameState state, LockerItem picked) {
    final s = context.read<AppServices>();
    final p = state.profile;
    final owned = state.isOwned(picked.id);
    final equipped = p.selectedCharacter == picked.id;
    return Column(children: [
      Row(children: [
        for (final c in Items.characters) ...[
          if (c != Items.characters.first) const SizedBox(width: 10),
          Expanded(
            child: Semantics(
              button: true,
              selected: c.id == picked.id,
              label: '${c.name}, ${state.isOwned(c.id) ? (p.selectedCharacter == c.id ? 'equipped' : 'owned') : 'locked'}',
              excludeSemantics: true,
              child: GestureDetector(
                onTap: () {
                  s.sounds.tap();
                  setState(() => _picked = c.id);
                },
                child: AnimatedContainer(
                  duration: UrMotion.fast,
                  height: 146,
                  decoration: BoxDecoration(
                    color: Color(c.accent),
                    borderRadius: BorderRadius.circular(18),
                    border: Border.all(width: 2, color: c.id == picked.id ? UrColors.text : Colors.transparent),
                  ),
                  clipBehavior: Clip.antiAlias,
                  child: Stack(children: [
                    Positioned(left: -10, right: -10, top: 4, child: Image.asset(c.image!, fit: BoxFit.fitWidth)),
                    Positioned(
                      left: 0,
                      right: 0,
                      bottom: 0,
                      child: Container(
                        color: UrColors.night,
                        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 7),
                        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                          Text(c.name, style: UrText.cardTitle.copyWith(fontSize: 14)),
                          Text(
                            p.selectedCharacter == c.id ? 'Equipped' : state.isOwned(c.id) ? 'Owned' : c.unlock == UnlockKind.level ? 'Level ${c.level}' : '${c.price} sparks',
                            style: UrText.small.copyWith(fontSize: 11, color: p.selectedCharacter == c.id ? UrColors.success : state.isOwned(c.id) ? UrColors.subtle : UrColors.amber),
                          ),
                        ]),
                      ),
                    ),
                  ]),
                ),
              ),
            ),
          ),
        ],
      ]),
      const SizedBox(height: 16),
      if (equipped)
        _statusButton('Equipped', UrColors.success)
      else if (owned)
        PressButton(label: 'Equip ${picked.name}', onPressed: () => state.equip(picked))
      else if (picked.unlock == UnlockKind.sparks)
        PressButton(
          label: 'Unlock for ${CountUp.format(picked.price)} sparks',
          icon: PhosphorIcons.sparkle(PhosphorIconsStyle.fill),
          onPressed: p.sparks >= picked.price
              ? () async {
                  if (await state.buyItem(picked)) {
                    s.sounds.reward();
                    await state.equip(picked);
                  }
                }
              : null,
        )
      else
        _statusButton('Unlocks at level ${picked.level}', UrColors.amber),
    ]);
  }

  Widget _statusButton(String text, Color color) => Container(
        height: 56,
        alignment: Alignment.center,
        decoration: BoxDecoration(borderRadius: BorderRadius.circular(UrRadius.button), border: Border.all(color: color, width: 1.5)),
        child: Text(text, style: UrText.button.copyWith(color: color, fontSize: 17)),
      );

  Widget _trail(GameState state, LockerItem t) {
    final owned = state.isOwned(t.id);
    final equipped = state.profile.selectedTrail == t.id;
    final status = equipped ? 'Equipped' : owned ? 'Equip' : t.unlock == UnlockKind.level ? 'Level ${t.level}' : 'Starter Pack';
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Panel(
        onTap: owned && !equipped ? () => state.equip(t) : null,
        semanticsLabel: '${t.name} trail, $status',
        child: Row(children: [
          Container(
            width: 56,
            height: 8,
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(4),
              gradient: t.id == Items.neonRibbon.id ? const LinearGradient(colors: [UrColors.magenta, UrColors.cyan]) : null,
              color: t.id == Items.neonRibbon.id ? null : Color(t.accent),
            ),
          ),
          const SizedBox(width: 14),
          Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(t.name, style: UrText.cardTitle.copyWith(fontSize: 15)),
            Text(t.tagline, style: UrText.small),
          ])),
          Text(status, style: UrText.label.copyWith(color: equipped ? UrColors.success : owned ? UrColors.magenta : UrColors.amber, fontWeight: FontWeight.w700)),
        ]),
      ),
    );
  }

  Widget _upgrade(GameState state, PowerUp u) {
    final level = state.profile.upgrades[Upgrades.key(u)] ?? 0;
    final cost = Upgrades.nextCost(level);
    final canBuy = cost != null && state.profile.sparks >= cost;
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Panel(
        child: Row(children: [
          Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text.rich(TextSpan(style: UrText.cardTitle.copyWith(fontSize: 15), children: [
              TextSpan(text: Upgrades.label(u)),
              TextSpan(text: ' · ${Upgrades.duration(u, level).toStringAsFixed(Upgrades.duration(u, level) % 1 == 0 ? 0 : 1)} s', style: UrText.small),
            ])),
            const SizedBox(height: 8),
            Semantics(
              label: 'Level $level of ${Upgrades.maxLevel}',
              excludeSemantics: true,
              child: Row(children: [
                for (var i = 0; i < Upgrades.maxLevel; i++) ...[
                  Container(width: 26, height: 6, decoration: BoxDecoration(color: i < level ? UrColors.cyan : UrColors.track, borderRadius: BorderRadius.circular(3))),
                  const SizedBox(width: 4),
                ],
              ]),
            ),
          ])),
          SizedBox(
            width: 96,
            height: 48,
            child: FilledButton(
              onPressed: canBuy
                  ? () async {
                      final sounds = context.read<AppServices>().sounds;
                      if (await state.buyUpgrade(u)) sounds.reward();
                    }
                  : null,
              style: FilledButton.styleFrom(
                backgroundColor: UrColors.cyan,
                foregroundColor: UrColors.cyanInk,
                disabledBackgroundColor: UrColors.track,
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                textStyle: UrText.label.copyWith(fontWeight: FontWeight.w800, fontSize: 14),
              ),
              child: Text(cost == null ? 'Max' : CountUp.format(cost)),
            ),
          ),
        ]),
      ),
    );
  }
}
