import 'dart:async';

import 'package:in_app_purchase/in_app_purchase.dart' as iap;
import 'package:in_app_purchase_android/billing_client_wrappers.dart';
import 'package:in_app_purchase_android/in_app_purchase_android.dart';

import '../log.dart';
import 'store.dart';

/// Google Play Billing through `in_app_purchase` (HANDOVER §7.5). Products must exist in Play Console
/// (owner task); until then [products] is empty and the Store screen shows prices as unavailable.
class PlayStore implements Store {
  PlayStore([iap.InAppPurchase? instance]) : _iap = instance ?? iap.InAppPurchase.instance {
    _sub = _iap.purchaseStream.listen(_onPurchases, onError: (Object e) => Log.write(Log.error, 'iap stream: $e'));
  }

  final iap.InAppPurchase _iap;
  late final StreamSubscription<List<iap.PurchaseDetails>> _sub;
  final _updates = StreamController<PurchaseUpdate>.broadcast();
  final _details = <String, iap.ProductDetails>{};
  final _pendingComplete = <String, iap.PurchaseDetails>{}; // token → details to complete
  final _consumables = <String>{}; // product ids bought as consumables (none yet: both products are one-time)

  @override
  Stream<PurchaseUpdate> get updates => _updates.stream;

  @override
  Future<bool> get available => _iap.isAvailable();

  @override
  Future<List<StoreProduct>> products(Set<String> ids) async {
    final r = await _iap.queryProductDetails(ids);
    if (r.error != null) Log.write(Log.error, 'iap products: ${r.error!.message}');
    for (final p in r.productDetails) {
      _details[p.id] = p;
    }
    return [for (final p in r.productDetails) StoreProduct(id: p.id, price: p.price)];
  }

  @override
  Future<void> buy(String productId, {required bool consumable}) async {
    var p = _details[productId];
    if (p == null) {
      await products({productId});
      p = _details[productId];
    }
    if (p == null) {
      _updates.add(PurchaseUpdate(productId: productId, status: PurchaseStatus.error, error: 'product unavailable'));
      return;
    }
    final param = iap.PurchaseParam(productDetails: p);
    if (consumable) _consumables.add(productId);
    // Granting and completing happen in Purchases once the update arrives, so autoConsume is off.
    final started = consumable ? await _iap.buyConsumable(purchaseParam: param, autoConsume: false) : await _iap.buyNonConsumable(purchaseParam: param);
    if (!started) _updates.add(PurchaseUpdate(productId: productId, status: PurchaseStatus.error, error: 'could not start'));
  }

  @override
  Future<void> restore() => _iap.restorePurchases();

  void _onPurchases(List<iap.PurchaseDetails> list) {
    for (final d in list) {
      final token = d.verificationData.serverVerificationData.isNotEmpty ? d.verificationData.serverVerificationData : d.purchaseID;
      final status = switch (d.status) {
        iap.PurchaseStatus.pending => PurchaseStatus.pending,
        iap.PurchaseStatus.purchased => PurchaseStatus.purchased,
        iap.PurchaseStatus.restored => PurchaseStatus.restored,
        iap.PurchaseStatus.canceled => PurchaseStatus.canceled,
        iap.PurchaseStatus.error => PurchaseStatus.error,
      };
      if (token != null && d.pendingCompletePurchase) _pendingComplete[token] = d;
      _updates.add(PurchaseUpdate(productId: d.productID, status: status, token: token, error: d.error?.message));
    }
  }

  @override
  Future<void> complete(PurchaseUpdate update) async {
    final d = update.token == null ? null : _pendingComplete.remove(update.token);
    if (d == null) return;
    if (d.status == iap.PurchaseStatus.purchased && _consumables.contains(d.productID)) {
      // Consumables are consumed only after they were granted, so a crash in between can't lose them.
      final r = await _iap.getPlatformAddition<InAppPurchaseAndroidPlatformAddition>().consumePurchase(d);
      if (r.responseCode != BillingResponse.ok) Log.write(Log.error, 'iap consume: ${r.responseCode}');
    }
    await _iap.completePurchase(d);
  }

  void dispose() {
    _sub.cancel();
    _updates.close();
  }
}
