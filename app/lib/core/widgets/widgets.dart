import 'package:flutter/material.dart';
import 'package:urban_runner/core/widgets/phosphor.dart';

import '../theme/tokens.dart';

/// "Reduce motion" for the whole widget tree (HANDOVER §4.1).
class MotionScope extends InheritedWidget {
  const MotionScope({super.key, required this.reduceMotion, required super.child});
  final bool reduceMotion;

  static bool reduced(BuildContext context) =>
      context.dependOnInheritedWidgetOfExactType<MotionScope>()?.reduceMotion == true || MediaQuery.of(context).disableAnimations;

  @override
  bool updateShouldNotify(MotionScope old) => old.reduceMotion != reduceMotion;
}

/// Primary call to action: solid colour on a darker base that "presses down" (approved design).
class PressButton extends StatefulWidget {
  const PressButton({
    super.key,
    required this.label,
    required this.onPressed,
    this.icon,
    this.color = UrColors.magenta,
    this.shade = UrColors.magentaShade,
    this.ink = UrColors.magentaInk,
    this.height = 60,
    this.subtitle,
    this.semanticsLabel,
  });

  final String label;
  final String? subtitle;
  final IconData? icon;
  final VoidCallback? onPressed;
  final Color color, shade, ink;
  final double height;
  final String? semanticsLabel;

  @override
  State<PressButton> createState() => _PressButtonState();
}

class _PressButtonState extends State<PressButton> {
  bool _down = false;

  @override
  Widget build(BuildContext context) {
    final enabled = widget.onPressed != null;
    const depth = 6.0;
    final offset = _down ? depth - 2 : 0.0;
    return Semantics(
      button: true,
      enabled: enabled,
      label: widget.semanticsLabel ?? widget.label,
      excludeSemantics: true,
      child: GestureDetector(
        onTapDown: enabled ? (_) => setState(() => _down = true) : null,
        onTapCancel: enabled ? () => setState(() => _down = false) : null,
        onTapUp: enabled ? (_) => setState(() => _down = false) : null,
        onTap: widget.onPressed,
        child: Opacity(
          opacity: enabled ? 1 : 0.45,
          child: SizedBox(
            height: widget.height + depth,
            child: Stack(children: [
              Positioned(
                left: 0, right: 0, bottom: 0, height: widget.height,
                child: DecoratedBox(decoration: BoxDecoration(color: widget.shade, borderRadius: BorderRadius.circular(UrRadius.button))),
              ),
              AnimatedPositioned(
                duration: const Duration(milliseconds: 70),
                left: 0, right: 0, top: offset, height: widget.height,
                child: DecoratedBox(
                  decoration: BoxDecoration(color: widget.color, borderRadius: BorderRadius.circular(UrRadius.button)),
                  child: Row(mainAxisAlignment: MainAxisAlignment.center, children: [
                    if (widget.icon != null) ...[Icon(widget.icon, color: widget.ink, size: 20), const SizedBox(width: 10)],
                    Flexible(
                      child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.center, children: [
                        Text(widget.label, style: UrText.button.copyWith(color: widget.ink), maxLines: 1, overflow: TextOverflow.ellipsis),
                        if (widget.subtitle != null)
                          Text(widget.subtitle!, style: UrText.small.copyWith(color: widget.ink.withValues(alpha: 0.72)), maxLines: 1, overflow: TextOverflow.ellipsis),
                      ]),
                    ),
                  ]),
                ),
              ),
            ]),
          ),
        ),
      ),
    );
  }
}

/// Solid panel with a hairline border (no glassmorphism, per the approved design).
class Panel extends StatelessWidget {
  const Panel({super.key, required this.child, this.padding = const EdgeInsets.all(14), this.color = UrColors.surface, this.radius = 20, this.onTap, this.semanticsLabel});
  final Widget child;
  final EdgeInsetsGeometry padding;
  final Color color;
  final double radius;
  final VoidCallback? onTap;
  final String? semanticsLabel;

  @override
  Widget build(BuildContext context) {
    final body = Container(
      padding: padding,
      decoration: BoxDecoration(color: color, borderRadius: BorderRadius.circular(radius), border: Border.all(color: UrColors.hairline)),
      child: child,
    );
    if (onTap == null) return body;
    return Semantics(
      button: true,
      label: semanticsLabel,
      child: Material(
        color: Colors.transparent,
        child: InkWell(borderRadius: BorderRadius.circular(radius), onTap: onTap, child: body),
      ),
    );
  }
}

class IconSquareButton extends StatelessWidget {
  const IconSquareButton({super.key, required this.icon, required this.label, required this.onTap});
  final IconData icon;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) => Semantics(
        button: true,
        label: label,
        child: Material(
          color: UrColors.surface,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14), side: const BorderSide(color: UrColors.hairline)),
          child: InkWell(
            borderRadius: BorderRadius.circular(14),
            onTap: onTap,
            child: SizedBox(width: 48, height: 48, child: Icon(icon, size: 21, color: UrColors.text)),
          ),
        ),
      );
}

/// Level ring: progress to the next level around the level number.
class LevelRing extends StatelessWidget {
  const LevelRing({super.key, required this.level, required this.progress, this.size = 42});
  final int level;
  final double progress;
  final double size;

  @override
  Widget build(BuildContext context) => Semantics(
        label: 'Courier level $level, ${(progress * 100).round()}% to the next level',
        excludeSemantics: true,
        child: SizedBox(
          width: size,
          height: size,
          child: Stack(alignment: Alignment.center, children: [
            SizedBox.expand(
              child: CircularProgressIndicator(value: progress.clamp(0, 1), strokeWidth: 3.5, color: UrColors.success, backgroundColor: UrColors.track),
            ),
            Text('$level', style: UrText.number.copyWith(fontSize: 15)),
          ]),
        ),
      );
}

class SparksAmount extends StatelessWidget {
  const SparksAmount({super.key, required this.value, this.size = 17});
  final int value;
  final double size;

  @override
  Widget build(BuildContext context) => Semantics(
        label: '$value sparks',
        excludeSemantics: true,
        child: Row(mainAxisSize: MainAxisSize.min, children: [
          Icon(PhosphorIcons.sparkle(PhosphorIconsStyle.fill), size: size * 0.85, color: UrColors.cyan),
          const SizedBox(width: 5),
          CountUp(value: value, style: UrText.number.copyWith(fontSize: size)),
        ]),
      );
}

/// Numbers count up (HANDOVER §4.1); instant with Reduce motion.
class CountUp extends StatelessWidget {
  const CountUp({super.key, required this.value, required this.style, this.duration = const Duration(milliseconds: 700), this.from = 0, this.suffix = ''});
  final int value;
  final int from;
  final TextStyle style;
  final Duration duration;
  final String suffix;

  static String format(int v) {
    final s = v.abs().toString();
    final b = StringBuffer(v < 0 ? '-' : '');
    for (var i = 0; i < s.length; i++) {
      if (i > 0 && (s.length - i) % 3 == 0) b.write(',');
      b.write(s[i]);
    }
    return b.toString();
  }

  @override
  Widget build(BuildContext context) {
    if (MotionScope.reduced(context)) return Text('${format(value)}$suffix', style: style);
    return TweenAnimationBuilder<double>(
      key: ValueKey(value),
      tween: Tween(begin: from.toDouble(), end: value.toDouble()),
      duration: duration,
      curve: Curves.easeOutCubic,
      builder: (_, v, _) => Text('${format(v.round())}$suffix', style: style),
    );
  }
}

/// Slightly tilted sticker label (a human touch from the approved design).
class Sticker extends StatelessWidget {
  const Sticker({super.key, required this.text, this.color = UrColors.amber, this.ink = UrColors.amberInk, this.angle = 0.07});
  final String text;
  final Color color, ink;
  final double angle;

  @override
  Widget build(BuildContext context) => Transform.rotate(
        angle: angle,
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 7),
          decoration: BoxDecoration(color: color, borderRadius: BorderRadius.circular(8)),
          child: Text(text, style: UrText.small.copyWith(color: ink, fontWeight: FontWeight.w700)),
        ),
      );
}

class ProgressBar extends StatelessWidget {
  const ProgressBar({super.key, required this.value, this.color = UrColors.success, this.height = 6, this.extra = 0, this.extraColor});
  final double value;
  final double extra;
  final Color color;
  final Color? extraColor;
  final double height;

  @override
  Widget build(BuildContext context) => ClipRRect(
        borderRadius: BorderRadius.circular(height),
        child: SizedBox(
          height: height,
          child: LayoutBuilder(builder: (context, c) {
            final w = c.maxWidth;
            final a = (value.clamp(0, 1) * w).toDouble();
            final b = (extra.clamp(0, 1 - value.clamp(0, 1)) * w).toDouble();
            return Stack(children: [
              Container(color: UrColors.track),
              AnimatedContainer(duration: UrMotion.slow, width: a, color: color),
              Positioned(left: a, child: AnimatedContainer(duration: UrMotion.slow, width: b, height: height, color: extraColor ?? color.withValues(alpha: 0.5))),
            ]);
          }),
        ),
      );
}

/// Subtle film grain over a screen (painted once, cached).
class Grain extends StatelessWidget {
  const Grain({super.key});
  @override
  Widget build(BuildContext context) => const IgnorePointer(child: RepaintBoundary(child: CustomPaint(painter: _GrainPainter(), size: Size.infinite)));
}

class _GrainPainter extends CustomPainter {
  const _GrainPainter();
  @override
  void paint(Canvas canvas, Size size) {
    final p = Paint()..color = const Color(0x0AFFFFFF);
    var seed = 1234567;
    final count = (size.width * size.height / 90).round();
    for (var i = 0; i < count; i++) {
      seed = (seed * 1103515245 + 12345) & 0x7FFFFFFF;
      final x = (seed % 10000) / 10000 * size.width;
      seed = (seed * 1103515245 + 12345) & 0x7FFFFFFF;
      final y = (seed % 10000) / 10000 * size.height;
      canvas.drawRect(Rect.fromLTWH(x, y, 1, 1), p);
    }
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}
