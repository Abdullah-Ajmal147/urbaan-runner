import 'dart:async';

import 'package:flutter/foundation.dart';

import '../../core/analytics/analytics.dart';
import '../../core/iap/store.dart';
import '../../core/log.dart';
import '../../games/urban_runner/game_state.dart';

/// The purchase state machine (HANDOVER §7.5): buy, cancel, pending, error and restore.
/// Every update is granted at most once per token and then completed so Play doesn't refund it.
class Purchases extends ChangeNotifier {
  Purchases({required this.store, required this.state, required this.analytics}) {
    _sub = store.updates.listen(_onUpdate);
  }

  final Store store;
  final GameState state;
  final Analytics analytics;
  late final StreamSubscription<PurchaseUpdate> _sub;

  bool busy = false;
  final pending = <String>{};

  /// Shows messages to the player; set by the app shell (a ScaffoldMessenger key).
  void Function(String message)? notify;

  Future<void> buy(String productId, {required bool consumable}) async {
    busy = true;
    notifyListeners();
    analytics.log('iap_start', {'product_id': productId});
    try {
      await store.buy(productId, consumable: consumable);
    } catch (e) {
      Log.write(Log.error, 'buy $productId failed: $e');
      analytics.log('iap_fail', {'product_id': productId});
      busy = false;
      notifyListeners();
      notify?.call("The store isn't reachable right now. Nothing was charged.");
    }
  }

  /// Restores non-consumables (Remove Ads, the Starter Pack's Lantern trail). Also called quietly at start-up
  /// to read entitlements from Play (§7.5).
  Future<void> restore({bool quiet = false}) async {
    if (!quiet) {
      busy = true;
      notifyListeners();
    }
    try {
      await store.restore();
      if (!quiet) notify?.call('Purchases restored.');
    } catch (e) {
      Log.write(Log.error, 'restore failed: $e');
      if (!quiet) notify?.call("Couldn't reach the store. Try again later.");
    } finally {
      if (!quiet) {
        busy = false;
        notifyListeners();
      }
    }
  }

  Future<void> _onUpdate(PurchaseUpdate u) async {
    switch (u.status) {
      case PurchaseStatus.pending:
        pending.add(u.productId);
        notify?.call("Waiting for your payment to clear. We'll add it as soon as it does.");
      case PurchaseStatus.purchased:
      case PurchaseStatus.restored:
        pending.remove(u.productId);
        final granted = await state.grantPurchase(u);
        await store.complete(u); // always acknowledge, even if this token was already granted
        if (granted && u.status == PurchaseStatus.purchased) {
          analytics.log('iap_complete', {'product_id': u.productId});
          notify?.call(u.productId == Products.removeAds ? 'Ads removed. Thank you!' : 'Starter Pack added: 2,000 sparks and the Lantern trail.');
        }
      case PurchaseStatus.canceled:
        analytics.log('iap_cancel', {'product_id': u.productId});
      case PurchaseStatus.error:
        analytics.log('iap_fail', {'product_id': u.productId});
        notify?.call('The purchase didn\'t go through. Nothing was charged.');
    }
    busy = false;
    notifyListeners();
  }

  @override
  void dispose() {
    _sub.cancel();
    super.dispose();
  }
}
