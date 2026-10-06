import 'package:flutter/material.dart';
import 'package:urban_runner/core/widgets/phosphor.dart';
import 'package:provider/provider.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../core/app_services.dart';
import '../../core/bridge/bridge_message.dart';
import '../../core/bridge/unity_bridge.dart';
import '../../core/theme/tokens.dart';
import '../../games/urban_runner/game_state.dart';
import '../../games/urban_runner/profile.dart';
import '../store/purchases.dart';

/// Privacy policy URL: owner task 9. Until it exists the row explains that.
const kPrivacyPolicyUrl = String.fromEnvironment('PRIVACY_POLICY_URL');

class SettingsScreen extends StatelessWidget {
  const SettingsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final state = context.watch<GameState>();
    final s = context.read<AppServices>();
    final st = state.profile.settings;

    Future<void> set(PlayerSettings next) async {
      await state.updateSettings(next);
      s.sounds
        ..sfx = next.sfx
        ..haptics = next.haptics;
      // Forward to Unity right away (settings.update), if it's running.
      if (s.bridge.state == BridgeState.ready) s.bridge.send(BridgeMessage('settings.update', payload: next.toUnity()));
    }

    return Scaffold(
      appBar: AppBar(backgroundColor: UrColors.night, title: const Text('Settings', style: UrText.cardTitle)),
      body: ListView(padding: const EdgeInsets.fromLTRB(16, 4, 16, 32), children: [
        const _Header('Sound and feel'),
        _Toggle(icon: PhosphorIcons.musicNotes(PhosphorIconsStyle.fill), label: 'Music', value: st.music, onChanged: (v) => set(st.copyWith(music: v))),
        _Toggle(icon: PhosphorIcons.speakerHigh(PhosphorIconsStyle.fill), label: 'Sound effects', value: st.sfx, onChanged: (v) => set(st.copyWith(sfx: v))),
        _Toggle(icon: PhosphorIcons.vibrate(PhosphorIconsStyle.fill), label: 'Vibration', value: st.haptics, onChanged: (v) => set(st.copyWith(haptics: v))),
        _Toggle(icon: PhosphorIcons.personSimple(PhosphorIconsStyle.fill), label: 'Reduce motion', sub: 'No camera shake, calmer animations', value: st.reduceMotion, onChanged: (v) => set(st.copyWith(reduceMotion: v))),
        const _Header('Privacy'),
        _Toggle(icon: PhosphorIcons.chartBar(PhosphorIconsStyle.fill), label: 'Share usage data', sub: 'Anonymous stats that help us improve the game', value: st.shareUsageData, onChanged: (v) => set(st.copyWith(shareUsageData: v))),
        if (s.ads.privacyOptionsRequired)
          _Row(icon: PhosphorIcons.shieldCheck(PhosphorIconsStyle.fill), label: 'Privacy options', sub: 'Change your ads consent', onTap: () => s.ads.showPrivacyOptions()),
        _Row(
          icon: PhosphorIcons.fileText(PhosphorIconsStyle.fill),
          label: 'Privacy policy',
          sub: kPrivacyPolicyUrl.isEmpty ? 'Published before launch' : null,
          onTap: kPrivacyPolicyUrl.isEmpty ? null : () => launchUrl(Uri.parse(kPrivacyPolicyUrl), mode: LaunchMode.externalApplication),
        ),
        const _Header('Game'),
        _Row(
          icon: PhosphorIcons.graduationCap(PhosphorIconsStyle.fill),
          label: 'Replay tutorial',
          sub: 'Your next run starts with the lesson',
          onTap: () async {
            await state.replayTutorial();
            if (context.mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Your next run will start with the tutorial.')));
          },
        ),
        _Row(icon: PhosphorIcons.arrowCounterClockwise(PhosphorIconsStyle.bold), label: 'Restore purchases', onTap: () => context.read<Purchases>().restore()),
        _Row(
          icon: PhosphorIcons.trash(PhosphorIconsStyle.fill),
          label: 'Reset progress',
          danger: true,
          sub: 'Sparks, levels, missions and unlocks. Purchases stay.',
          onTap: () async {
            final ok = await showDialog<bool>(
              context: context,
              builder: (c) => AlertDialog(
                title: const Text('Reset all progress?', style: UrText.cardTitle),
                content: const Text('Your sparks, level, missions, upgrades and unlocked couriers go back to the start. Purchases are kept. This can\'t be undone.', style: UrText.body),
                actions: [
                  TextButton(onPressed: () => Navigator.pop(c, false), child: const Text('Keep my progress')),
                  FilledButton(
                    style: FilledButton.styleFrom(backgroundColor: UrColors.danger, foregroundColor: Colors.white),
                    onPressed: () => Navigator.pop(c, true),
                    child: const Text('Reset'),
                  ),
                ],
              ),
            );
            if (ok == true) {
              await state.resetProgress();
              if (context.mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Progress reset.')));
            }
          },
        ),
        const SizedBox(height: 24),
        Center(child: Text('Urban Runner ${s.buildVersion}', style: UrText.small)),
      ]),
    );
  }
}

class _Header extends StatelessWidget {
  const _Header(this.text);
  final String text;
  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.fromLTRB(4, 22, 4, 8),
        child: Semantics(header: true, child: Text(text.toUpperCase(), style: UrText.small.copyWith(letterSpacing: 1.2, fontWeight: FontWeight.w700))),
      );
}

class _Toggle extends StatelessWidget {
  const _Toggle({required this.icon, required this.label, required this.value, required this.onChanged, this.sub});
  final IconData icon;
  final String label;
  final String? sub;
  final bool value;
  final ValueChanged<bool> onChanged;

  @override
  Widget build(BuildContext context) => _Tile(
        icon: icon,
        label: label,
        sub: sub,
        trailing: Switch(value: value, onChanged: onChanged),
        onTap: () => onChanged(!value),
        toggled: value,
      );
}

class _Row extends StatelessWidget {
  const _Row({required this.icon, required this.label, this.onTap, this.sub, this.danger = false});
  final IconData icon;
  final String label;
  final String? sub;
  final VoidCallback? onTap;
  final bool danger;

  @override
  Widget build(BuildContext context) => _Tile(
        icon: icon,
        label: label,
        sub: sub,
        danger: danger,
        onTap: onTap,
        trailing: onTap == null ? null : Icon(PhosphorIcons.caretRight(PhosphorIconsStyle.bold), color: UrColors.faint, size: 18),
      );
}

class _Tile extends StatelessWidget {
  const _Tile({required this.icon, required this.label, this.sub, this.trailing, this.onTap, this.danger = false, this.toggled});
  final IconData icon;
  final String label;
  final String? sub;
  final Widget? trailing;
  final VoidCallback? onTap;
  final bool danger;
  final bool? toggled;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(bottom: 8),
        child: Semantics(
          toggled: toggled,
          button: toggled == null && onTap != null,
          label: sub == null ? label : '$label. $sub',
          excludeSemantics: true,
          child: Material(
            color: UrColors.surface,
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16), side: const BorderSide(color: UrColors.hairline)),
            child: InkWell(
              borderRadius: BorderRadius.circular(16),
              onTap: onTap,
              child: ConstrainedBox(
                constraints: const BoxConstraints(minHeight: 60),
                child: Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
                  child: Row(children: [
                    Icon(icon, color: danger ? UrColors.danger : UrColors.muted, size: 20),
                    const SizedBox(width: 14),
                    Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                      Text(label, style: UrText.label.copyWith(fontSize: 15, color: danger ? UrColors.danger : UrColors.text)),
                      if (sub != null) Text(sub!, style: UrText.small),
                    ])),
                    ?trailing,
                  ]),
                ),
              ),
            ),
          ),
        ),
      );
}
