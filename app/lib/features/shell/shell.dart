import 'package:flutter/material.dart';
import 'package:urban_runner/core/widgets/phosphor.dart';
import 'package:provider/provider.dart';

import '../../core/app_services.dart';
import '../../core/theme/tokens.dart';
import '../../games/urban_runner/game_state.dart';
import '../home/home_screen.dart';
import '../locker/locker_screen.dart';
import '../missions/missions_screen.dart';
import '../store/store_screen.dart';

/// Bottom bar: Home · Missions · Locker · Store (HANDOVER §4.2).
class Shell extends StatefulWidget {
  const Shell({super.key});
  @override
  State<Shell> createState() => _ShellState();
}

class _ShellState extends State<Shell> {
  int _tab = 0;

  void _go(int i) {
    if (i == _tab) return;
    context.read<AppServices>().sounds.tap();
    setState(() => _tab = i);
  }

  @override
  Widget build(BuildContext context) {
    final claimable = context.select<GameState, int>((s) => s.profile.missions.where((m) => m.claimable).length);
    final pages = [
      HomeScreen(onOpenMissions: () => _go(1)),
      const MissionsScreen(),
      const LockerScreen(),
      const StoreScreen(),
    ];
    return Scaffold(
      body: AnimatedSwitcher(duration: UrMotion.normal, child: KeyedSubtree(key: ValueKey(_tab), child: pages[_tab])),
      bottomNavigationBar: DecoratedBox(
        decoration: const BoxDecoration(color: UrColors.deep, border: Border(top: BorderSide(color: UrColors.hairline))),
        child: SafeArea(
          top: false,
          child: SizedBox(
            height: 66,
            child: Row(children: [
              _Tab(label: 'Home', icon: PhosphorIcons.house, selected: _tab == 0, onTap: () => _go(0)),
              _Tab(label: 'Missions', icon: PhosphorIcons.target, selected: _tab == 1, onTap: () => _go(1), badge: claimable),
              _Tab(label: 'Locker', icon: PhosphorIcons.tShirt, selected: _tab == 2, onTap: () => _go(2)),
              _Tab(label: 'Store', icon: PhosphorIcons.storefront, selected: _tab == 3, onTap: () => _go(3)),
            ]),
          ),
        ),
      ),
    );
  }
}

class _Tab extends StatelessWidget {
  const _Tab({required this.label, required this.icon, required this.selected, required this.onTap, this.badge = 0});
  final String label;
  final IconData Function([PhosphorIconsStyle]) icon;
  final bool selected;
  final VoidCallback onTap;
  final int badge;

  @override
  Widget build(BuildContext context) => Expanded(
        child: Semantics(
          selected: selected,
          button: true,
          label: badge > 0 ? '$label, $badge ready to claim' : label,
          excludeSemantics: true,
          child: InkResponse(
            onTap: onTap,
            child: Column(mainAxisAlignment: MainAxisAlignment.center, children: [
              Badge(
                isLabelVisible: badge > 0,
                label: Text('$badge'),
                backgroundColor: UrColors.success,
                textColor: UrColors.successInk,
                child: Icon(icon(selected ? PhosphorIconsStyle.fill : PhosphorIconsStyle.bold), size: 24, color: selected ? UrColors.magenta : UrColors.faint),
              ),
              const SizedBox(height: 4),
              Text(label, style: UrText.small.copyWith(fontSize: 11, color: selected ? UrColors.text : UrColors.faint, fontWeight: selected ? FontWeight.w700 : FontWeight.w600)),
            ]),
          ),
        ),
      );
}
