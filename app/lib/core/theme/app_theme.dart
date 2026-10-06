import 'package:flutter/material.dart';

import 'tokens.dart';

/// Dark-first Material 3 theme built from the shared tokens (HANDOVER §4.1).
ThemeData buildAppTheme() {
  final scheme = ColorScheme.fromSeed(seedColor: UrColors.magenta, brightness: Brightness.dark).copyWith(
    primary: UrColors.magenta,
    onPrimary: UrColors.magentaInk,
    secondary: UrColors.cyan,
    onSecondary: UrColors.cyanInk,
    tertiary: UrColors.amber,
    surface: UrColors.night,
    onSurface: UrColors.text,
    error: UrColors.danger,
  );
  return ThemeData(
    useMaterial3: true,
    colorScheme: scheme,
    scaffoldBackgroundColor: UrColors.night,
    fontFamily: UrFonts.body,
    splashFactory: InkSparkle.splashFactory,
    pageTransitionsTheme: const PageTransitionsTheme(builders: {
      TargetPlatform.android: FadeForwardsPageTransitionsBuilder(),
    }),
    textTheme: const TextTheme(
      headlineLarge: UrText.hero,
      headlineMedium: UrText.title,
      titleMedium: UrText.cardTitle,
      bodyMedium: UrText.body,
      labelLarge: UrText.label,
    ),
    dialogTheme: const DialogThemeData(
      backgroundColor: UrColors.surfaceRaised,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.all(Radius.circular(UrRadius.card))),
    ),
    switchTheme: SwitchThemeData(
      thumbColor: WidgetStateProperty.resolveWith((s) => s.contains(WidgetState.selected) ? UrColors.magentaInk : UrColors.subtle),
      trackColor: WidgetStateProperty.resolveWith((s) => s.contains(WidgetState.selected) ? UrColors.magenta : UrColors.track),
    ),
    snackBarTheme: const SnackBarThemeData(
      behavior: SnackBarBehavior.floating,
      backgroundColor: UrColors.surfaceRaised,
      contentTextStyle: UrText.label,
    ),
  );
}
