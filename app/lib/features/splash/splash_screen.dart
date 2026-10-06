import 'package:flutter/material.dart';

import '../../core/theme/tokens.dart';

/// Start-up (HANDOVER §4.2): wordmark while services start in parallel; never longer than 2 s.
class SplashScreen extends StatelessWidget {
  const SplashScreen({super.key});

  @override
  Widget build(BuildContext context) => Scaffold(
        backgroundColor: UrColors.night,
        body: Center(
          child: Semantics(
            label: 'Urban Runner',
            excludeSemantics: true,
            child: Column(mainAxisSize: MainAxisSize.min, children: [
              Container(
                width: 64,
                height: 80,
                decoration: BoxDecoration(color: UrColors.amber, borderRadius: BorderRadius.circular(32)),
                child: Center(child: Container(width: 6, height: 50, color: UrColors.amberInk.withValues(alpha: .35))),
              ),
              const SizedBox(height: 20),
              Text('Urban Runner', style: UrText.hero.copyWith(fontSize: 38)),
              const SizedBox(height: 6),
              const Text('Lantern City', style: UrText.small),
            ]),
          ),
        ),
      );
}
