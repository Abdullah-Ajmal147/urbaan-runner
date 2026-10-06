import 'package:flutter/widgets.dart';

/// Phosphor icons (MIT, @phosphor-icons/web 2.1.1) from the bundled icon fonts.
/// Replaces the phosphor_flutter package, which is unmaintained since 2024 and no longer compiles on current Flutter.
/// To add an icon: copy its codepoints from @phosphor-icons/web src/bold/style.css and src/fill/style.css.
enum PhosphorIconsStyle { bold, fill }

abstract final class PhosphorIcons {
  static IconData arrowClockwise([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe036, fontFamily: 'PhosphorFill') : const IconData(0xe036, fontFamily: 'PhosphorBold');
  static IconData arrowCounterClockwise([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe038, fontFamily: 'PhosphorFill') : const IconData(0xe038, fontFamily: 'PhosphorBold');
  static IconData arrowFatDown([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe518, fontFamily: 'PhosphorFill') : const IconData(0xe518, fontFamily: 'PhosphorBold');
  static IconData arrowFatUp([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe52e, fontFamily: 'PhosphorFill') : const IconData(0xe52e, fontFamily: 'PhosphorBold');
  static IconData arrowsLeftRight([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe0a0, fontFamily: 'PhosphorFill') : const IconData(0xe0a0, fontFamily: 'PhosphorBold');
  static IconData calendarDots([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe7b4, fontFamily: 'PhosphorFill') : const IconData(0xe7b4, fontFamily: 'PhosphorBold');
  static IconData caretRight([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe13a, fontFamily: 'PhosphorFill') : const IconData(0xe13a, fontFamily: 'PhosphorBold');
  static IconData chartBar([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe150, fontFamily: 'PhosphorFill') : const IconData(0xe150, fontFamily: 'PhosphorBold');
  static IconData check([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe182, fontFamily: 'PhosphorFill') : const IconData(0xe182, fontFamily: 'PhosphorBold');
  static IconData checkCircle([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe184, fontFamily: 'PhosphorFill') : const IconData(0xe184, fontFamily: 'PhosphorBold');
  static IconData fileText([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe23a, fontFamily: 'PhosphorFill') : const IconData(0xe23a, fontFamily: 'PhosphorBold');
  static IconData flagCheckered([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xea38, fontFamily: 'PhosphorFill') : const IconData(0xea38, fontFamily: 'PhosphorBold');
  static IconData gearSix([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe272, fontFamily: 'PhosphorFill') : const IconData(0xe272, fontFamily: 'PhosphorBold');
  static IconData gift([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe276, fontFamily: 'PhosphorFill') : const IconData(0xe276, fontFamily: 'PhosphorBold');
  static IconData graduationCap([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe62c, fontFamily: 'PhosphorFill') : const IconData(0xe62c, fontFamily: 'PhosphorBold');
  static IconData handSwipeRight([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xec92, fontFamily: 'PhosphorFill') : const IconData(0xec92, fontFamily: 'PhosphorBold');
  static IconData heartbeat([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe2ac, fontFamily: 'PhosphorFill') : const IconData(0xe2ac, fontFamily: 'PhosphorBold');
  static IconData house([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe2c2, fontFamily: 'PhosphorFill') : const IconData(0xe2c2, fontFamily: 'PhosphorBold');
  static IconData lightning([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe2de, fontFamily: 'PhosphorFill') : const IconData(0xe2de, fontFamily: 'PhosphorBold');
  static IconData lockOpen([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe306, fontFamily: 'PhosphorFill') : const IconData(0xe306, fontFamily: 'PhosphorBold');
  static IconData magnet([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe680, fontFamily: 'PhosphorFill') : const IconData(0xe680, fontFamily: 'PhosphorBold');
  static IconData musicNotes([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe340, fontFamily: 'PhosphorFill') : const IconData(0xe340, fontFamily: 'PhosphorBold');
  static IconData personSimple([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe72e, fontFamily: 'PhosphorFill') : const IconData(0xe72e, fontFamily: 'PhosphorBold');
  static IconData personSimpleRun([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe730, fontFamily: 'PhosphorFill') : const IconData(0xe730, fontFamily: 'PhosphorBold');
  static IconData play([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe3d0, fontFamily: 'PhosphorFill') : const IconData(0xe3d0, fontFamily: 'PhosphorBold');
  static IconData shieldCheck([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe40c, fontFamily: 'PhosphorFill') : const IconData(0xe40c, fontFamily: 'PhosphorBold');
  static IconData sparkle([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe6a2, fontFamily: 'PhosphorFill') : const IconData(0xe6a2, fontFamily: 'PhosphorBold');
  static IconData speakerHigh([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe44a, fontFamily: 'PhosphorFill') : const IconData(0xe44a, fontFamily: 'PhosphorBold');
  static IconData storefront([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe470, fontFamily: 'PhosphorFill') : const IconData(0xe470, fontFamily: 'PhosphorBold');
  static IconData target([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe47c, fontFamily: 'PhosphorFill') : const IconData(0xe47c, fontFamily: 'PhosphorBold');
  static IconData trash([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe4a6, fontFamily: 'PhosphorFill') : const IconData(0xe4a6, fontFamily: 'PhosphorBold');
  static IconData trophy([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe67e, fontFamily: 'PhosphorFill') : const IconData(0xe67e, fontFamily: 'PhosphorBold');
  static IconData tShirt([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe670, fontFamily: 'PhosphorFill') : const IconData(0xe670, fontFamily: 'PhosphorBold');
  static IconData user([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe4c2, fontFamily: 'PhosphorFill') : const IconData(0xe4c2, fontFamily: 'PhosphorBold');
  static IconData vibrate([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe4d8, fontFamily: 'PhosphorFill') : const IconData(0xe4d8, fontFamily: 'PhosphorBold');
  static IconData warningCircle([PhosphorIconsStyle s = PhosphorIconsStyle.bold]) =>
      s == PhosphorIconsStyle.fill ? const IconData(0xe4e2, fontFamily: 'PhosphorFill') : const IconData(0xe4e2, fontFamily: 'PhosphorBold');

}
