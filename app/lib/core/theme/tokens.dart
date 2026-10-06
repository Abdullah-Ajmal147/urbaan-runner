import 'package:flutter/material.dart';

/// Design tokens shared with the Unity game (HANDOVER §3.1) and the approved v2 mockups.
abstract final class UrColors {
  static const night = Color(0xFF0E1230);
  static const deep = Color(0xFF0B0F29);
  static const surface = Color(0xFF151B45);
  static const surfaceRaised = Color(0xFF1A2050);
  static const plum = Color(0xFF22195A);
  static const plumDeep = Color(0xFF170F45);
  static const magenta = Color(0xFFFF3DA6);
  static const magentaShade = Color(0xFFB5196F);
  static const magentaInk = Color(0xFF14051F);
  static const cyan = Color(0xFF2EE6FF);
  static const cyanInk = Color(0xFF062A31);
  static const amber = Color(0xFFFFB547);
  static const amberInk = Color(0xFF2B1700);
  static const text = Color(0xFFF4F6FF);
  static const muted = Color(0xFFC3C7EA);
  static const subtle = Color(0xFF9AA0CC);
  static const faint = Color(0xFF7F86B8);
  static const success = Color(0xFF3DFFA2);
  static const successInk = Color(0xFF062417);
  static const danger = Color(0xFFFF5A5A);
  static const track = Color(0xFF262D63);
  static const hairline = Color(0x12F4F6FF);
}

abstract final class UrFonts {
  static const display = 'Bricolage';
  static const body = 'Instrument';
}

abstract final class UrRadius {
  static const card = 24.0;
  static const hero = 28.0;
  static const button = 18.0;
  static const chip = 12.0;
}

abstract final class UrMotion {
  static const fast = Duration(milliseconds: 150);
  static const normal = Duration(milliseconds: 220);
  static const slow = Duration(milliseconds: 300);
}

/// Text styles from the mockups. Display = Bricolage Grotesque, body = Instrument Sans.
abstract final class UrText {
  static const hero = TextStyle(fontFamily: UrFonts.display, fontWeight: FontWeight.w800, fontSize: 34, height: 1.0, letterSpacing: -0.6, color: UrColors.text);
  static const title = TextStyle(fontFamily: UrFonts.display, fontWeight: FontWeight.w800, fontSize: 28, height: 1.05, letterSpacing: -0.5, color: UrColors.text);
  static const cardTitle = TextStyle(fontFamily: UrFonts.display, fontWeight: FontWeight.w700, fontSize: 17, color: UrColors.text);
  static const number = TextStyle(fontFamily: UrFonts.display, fontWeight: FontWeight.w800, fontSize: 20, color: UrColors.text, fontFeatures: [FontFeature.tabularFigures()]);
  static const body = TextStyle(fontFamily: UrFonts.body, fontWeight: FontWeight.w400, fontSize: 14, height: 1.45, color: UrColors.muted);
  static const label = TextStyle(fontFamily: UrFonts.body, fontWeight: FontWeight.w600, fontSize: 13, color: UrColors.text);
  static const small = TextStyle(fontFamily: UrFonts.body, fontWeight: FontWeight.w600, fontSize: 12, color: UrColors.subtle);
  static const button = TextStyle(fontFamily: UrFonts.display, fontWeight: FontWeight.w800, fontSize: 19);
}
