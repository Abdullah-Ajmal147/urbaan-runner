/// Ads behind an interface (HANDOVER §7.4). The real implementation (Google Mobile Ads with test IDs
/// and the UMP consent flow) plugs in without changing the screens.
abstract class AdsService {
  Future<void> init();

  /// Runs Google's consent flow (UMP) if needed. Must complete before any ad request.
  Future<void> gatherConsent();

  /// Whether Settings must show a "Privacy options" entry (consent rules require one in some regions).
  bool get privacyOptionsRequired;
  Future<void> showPrivacyOptions();

  /// Shows a rewarded ad; resolves true only if the reward was earned.
  Future<bool> showRewarded(String placement);

  /// Shows an interstitial if one is loaded. Resolves when it is closed: true if one was shown, false
  /// (immediately) if none was ready, so pacing only counts ads the player actually saw.
  Future<bool> showInterstitial();
}

/// Pure interstitial rules (HANDOVER §7.4); every number comes from Remote Config (§7.3).
class InterstitialPolicy {
  const InterstitialPolicy({required this.firstAfterRuns, required this.everyNRuns, required this.minIntervalS});

  final int firstAfterRuns;
  final int everyNRuns;
  final int minIntervalS;

  /// [totalRuns] = runs finished ever; [runsSinceLast] = runs since the last interstitial (or since install);
  /// [lastShownMs] = 0 if never shown.
  bool shouldShow({
    required bool removeAds,
    required bool firstAppSession,
    required bool tutorialRun,
    required bool justWatchedRewarded,
    required int totalRuns,
    required int runsSinceLast,
    required int lastShownMs,
    required int nowMs,
  }) {
    if (removeAds || firstAppSession || tutorialRun || justWatchedRewarded) return false;
    if (totalRuns < firstAfterRuns) return false;
    if (lastShownMs > 0) {
      if (runsSinceLast < everyNRuns) return false;
      if (nowMs - lastShownMs < minIntervalS * 1000) return false;
    }
    return true;
  }
}
