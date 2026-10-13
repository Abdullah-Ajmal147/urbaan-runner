import 'dart:async';

import 'package:flutter/material.dart';

import '../theme/tokens.dart';
import 'ads.dart';

/// Stand-in for real ads until AdMob is wired (and in tests): a full-screen "test ad" that grants its reward.
class DebugAdsService implements AdsService {
  DebugAdsService({required this.navigatorKey, this.grant = true, this.showUi = true, this.adSeconds = 3});

  final GlobalKey<NavigatorState> navigatorKey;
  bool grant;
  bool showUi;
  int adSeconds;
  int rewardedShown = 0;
  int interstitialsShown = 0;
  /// False simulates "no interstitial loaded" (no fill, offline).
  bool interstitialReady = true;
  /// Set to a never-completing future to simulate Google's consent check hanging on a slow network.
  Future<void>? consentResult;

  @override
  Future<void> init() async => gatherConsent();

  @override
  Future<void> gatherConsent() => consentResult ?? Future<void>.value();

  @override
  bool get privacyOptionsRequired => false;

  @override
  Future<void> showPrivacyOptions() async {}

  @override
  Future<bool> showRewarded(String placement) async {
    rewardedShown++;
    if (showUi) await _show('Rewarded test ad · $placement');
    return grant;
  }

  @override
  Future<bool> showInterstitial() async {
    if (!interstitialReady) return false;
    interstitialsShown++;
    if (showUi) await _show('Interstitial test ad');
    return true;
  }

  Future<void> _show(String label) async {
    final nav = navigatorKey.currentState;
    if (nav == null) return;
    await nav.push(PageRouteBuilder<void>(
      opaque: true,
      settings: const RouteSettings(name: '/test-ad'),
      pageBuilder: (_, _, _) => _TestAd(label: label, seconds: adSeconds),
    ));
  }
}

class _TestAd extends StatefulWidget {
  const _TestAd({required this.label, required this.seconds});
  final String label;
  final int seconds;
  @override
  State<_TestAd> createState() => _TestAdState();
}

class _TestAdState extends State<_TestAd> {
  late int _left = widget.seconds;
  Timer? _t;

  @override
  void initState() {
    super.initState();
    _t = Timer.periodic(const Duration(seconds: 1), (_) {
      if (_left <= 1) {
        _t?.cancel();
        Navigator.of(context).pop();
      } else {
        setState(() => _left--);
      }
    });
  }

  @override
  void dispose() {
    _t?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        backgroundColor: Colors.white,
        body: Center(
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            Text(widget.label, style: UrText.cardTitle.copyWith(color: UrColors.night)),
            const SizedBox(height: 8),
            Text('Closes in $_left s', style: UrText.label.copyWith(color: UrColors.night)),
          ]),
        ),
      );
}
