# Drafts (not compiled)

Ready-to-wire code for M4 and M5. It lives here and not in `app/lib`, because the packages it imports (firebase_*, cloud_firestore, google_mobile_ads, in_app_purchase) aren't in `pubspec.yaml` yet. Move each file into place when its milestone adds the packages:

| File | Goes to | Milestone |
| --- | --- | --- |
| `admob_ads.dart` | `app/lib/core/ads/` | M5 |
| `play_store.dart` | `app/lib/core/iap/` | M5 |

`firebase_services.dart` was moved to `app/lib/core/firebase/` in M4 (2026-10-10).
