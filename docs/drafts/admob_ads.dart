import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:google_mobile_ads/google_mobile_ads.dart';

import '../log.dart';
import 'ads.dart';

/// Ad unit IDs. Debug and profile builds always use Google's official test units (HANDOVER §7.4); release builds
/// take real IDs only from `--dart-define` and fall back to test units when none are given.
abstract final class AdUnits {
  static const testRewarded = 'ca-app-pub-3940256099942544/5224354917';
  static const testInterstitial = 'ca-app-pub-3940256099942544/1033173712';

  static String get rewarded => _pick(const String.fromEnvironment('UR_ADMOB_REWARDED'), testRewarded);
  static String get interstitial => _pick(const String.fromEnvironment('UR_ADMOB_INTERSTITIAL'), testInterstitial);

  static String _pick(String real, String test) => kReleaseMode && real.isNotEmpty ? real : test;
  static bool get usingTestIds => rewarded == testRewarded && interstitial == testInterstitial;
}

/// Google Mobile Ads with UMP consent. Ads are requested only after consent says they may be.
class AdMobAdsService implements AdsService {
  AdMobAdsService({this.loadTimeout = const Duration(seconds: 8)});

  final Duration loadTimeout;
  bool _initialized = false;
  bool _canRequest = false;
  bool _privacyRequired = false;
  RewardedAd? _rewarded;
  InterstitialAd? _interstitial;

  @override
  Future<void> init() async {
    if (_initialized) return;
    _initialized = true;
    Log.write(Log.game, 'ads init (test ids: ${AdUnits.usingTestIds})');
    await gatherConsent();
    if (!_canRequest) return;
    await MobileAds.instance.initialize();
    _loadRewarded();
    _loadInterstitial();
  }

  @override
  Future<void> gatherConsent() {
    final done = Completer<void>();
    ConsentInformation.instance.requestConsentInfoUpdate(
      ConsentRequestParameters(),
      () async {
        await ConsentForm.loadAndShowConsentFormIfRequired((FormError? e) {
          if (e != null) Log.write(Log.error, 'consent form: ${e.message}');
        });
        await _refreshConsent();
        if (!done.isCompleted) done.complete();
      },
      (FormError e) async {
        Log.write(Log.error, 'consent info: ${e.message}');
        await _refreshConsent(); // a previous session's consent may still allow requests
        if (!done.isCompleted) done.complete();
      },
    );
    return done.future;
  }

  Future<void> _refreshConsent() async {
    _canRequest = await ConsentInformation.instance.canRequestAds();
    _privacyRequired = await ConsentInformation.instance.getPrivacyOptionsRequirementStatus() == PrivacyOptionsRequirementStatus.required;
  }

  @override
  bool get privacyOptionsRequired => _privacyRequired;

  @override
  Future<void> showPrivacyOptions() async {
    final done = Completer<void>();
    await ConsentForm.showPrivacyOptionsForm((FormError? e) {
      if (e != null) Log.write(Log.error, 'privacy options: ${e.message}');
      if (!done.isCompleted) done.complete();
    });
    await done.future;
    await _refreshConsent();
  }

  void _loadRewarded() {
    if (!_canRequest) return;
    RewardedAd.load(
      adUnitId: AdUnits.rewarded,
      request: const AdRequest(),
      rewardedAdLoadCallback: RewardedAdLoadCallback(
        onAdLoaded: (ad) => _rewarded = ad,
        onAdFailedToLoad: (e) => Log.write(Log.error, 'rewarded load: ${e.code} ${e.message}'),
      ),
    );
  }

  void _loadInterstitial() {
    if (!_canRequest) return;
    InterstitialAd.load(
      adUnitId: AdUnits.interstitial,
      request: const AdRequest(),
      adLoadCallback: InterstitialAdLoadCallback(
        onAdLoaded: (ad) => _interstitial = ad,
        onAdFailedToLoad: (e) => Log.write(Log.error, 'interstitial load: ${e.code} ${e.message}'),
      ),
    );
  }

  Future<T?> _await<T>(T? Function() get) async {
    final end = DateTime.now().add(loadTimeout);
    while (get() == null && DateTime.now().isBefore(end)) {
      await Future<void>.delayed(const Duration(milliseconds: 200));
    }
    return get();
  }

  @override
  Future<bool> showRewarded(String placement) async {
    if (!_initialized) await init();
    if (!_canRequest) return false;
    if (_rewarded == null) _loadRewarded();
    final ad = await _await(() => _rewarded);
    if (ad == null) return false;
    _rewarded = null;
    var earned = false;
    final closed = Completer<void>();
    ad.fullScreenContentCallback = FullScreenContentCallback(
      onAdDismissedFullScreenContent: (a) {
        a.dispose();
        if (!closed.isCompleted) closed.complete();
      },
      onAdFailedToShowFullScreenContent: (a, e) {
        Log.write(Log.error, 'rewarded show: ${e.message}');
        a.dispose();
        if (!closed.isCompleted) closed.complete();
      },
    );
    await ad.show(onUserEarnedReward: (_, _) => earned = true);
    await closed.future;
    _loadRewarded(); // ready for the next one
    return earned;
  }

  @override
  Future<void> showInterstitial() async {
    final ad = _interstitial;
    if (ad == null) {
      _loadInterstitial();
      return;
    }
    _interstitial = null;
    final closed = Completer<void>();
    ad.fullScreenContentCallback = FullScreenContentCallback(
      onAdDismissedFullScreenContent: (a) {
        a.dispose();
        if (!closed.isCompleted) closed.complete();
      },
      onAdFailedToShowFullScreenContent: (a, e) {
        a.dispose();
        if (!closed.isCompleted) closed.complete();
      },
    );
    await ad.show();
    await closed.future;
    _loadInterstitial();
  }
}
