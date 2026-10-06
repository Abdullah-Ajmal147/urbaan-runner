import 'dart:async';

/// In-app purchases behind an interface (HANDOVER §7.5). Real: Play Billing via in_app_purchase.
/// Tests and the emulator use [FakeStore].
enum PurchaseStatus { pending, purchased, restored, canceled, error }

class PurchaseUpdate {
  const PurchaseUpdate({required this.productId, required this.status, this.token, this.error});
  final String productId;
  final PurchaseStatus status;
  /// Unique per purchase; rewards are granted at most once per token.
  final String? token;
  final String? error;
}

class StoreProduct {
  const StoreProduct({required this.id, required this.price});
  final String id;
  /// Localised price from the store, e.g. "$1.99".
  final String price;
}

abstract class Store {
  Stream<PurchaseUpdate> get updates;
  Future<bool> get available;
  Future<List<StoreProduct>> products(Set<String> ids);
  Future<void> buy(String productId, {required bool consumable});
  Future<void> restore();

  /// Acknowledge/complete so Play doesn't refund it (§7.5). Called after granting.
  Future<void> complete(PurchaseUpdate update);
}

/// Scriptable store: tests decide what each buy() produces.
class FakeStore implements Store {
  FakeStore({this.prices = const {'remove_ads': '\$2.99', 'starter_pack': '\$1.99'}});

  final Map<String, String> prices;
  final _controller = StreamController<PurchaseUpdate>.broadcast();
  final completed = <String>[];
  final owned = <String, String>{}; // productId -> token (non-consumables, for restore)
  PurchaseStatus nextResult = PurchaseStatus.purchased;
  var _tokens = 0;

  @override
  Stream<PurchaseUpdate> get updates => _controller.stream;

  @override
  Future<bool> get available async => true;

  @override
  Future<List<StoreProduct>> products(Set<String> ids) async =>
      [for (final id in ids) if (prices[id] != null) StoreProduct(id: id, price: prices[id]!)];

  @override
  Future<void> buy(String productId, {required bool consumable}) async {
    final status = nextResult;
    final token = status == PurchaseStatus.purchased || status == PurchaseStatus.pending ? 'tok_${productId}_${++_tokens}' : null;
    if (status == PurchaseStatus.purchased && token != null) owned[productId] = token;
    _controller.add(PurchaseUpdate(productId: productId, status: status, token: token, error: status == PurchaseStatus.error ? 'store error' : null));
  }

  /// Completes a pending purchase (e.g. the payment cleared later).
  void resolvePending(String productId, String token) {
    owned[productId] = token;
    _controller.add(PurchaseUpdate(productId: productId, status: PurchaseStatus.purchased, token: token));
  }

  /// Simulates the store re-delivering an update (duplicate callback).
  void redeliver(PurchaseUpdate u) => _controller.add(u);

  @override
  Future<void> restore() async {
    for (final e in owned.entries) {
      _controller.add(PurchaseUpdate(productId: e.key, status: PurchaseStatus.restored, token: e.value));
    }
  }

  @override
  Future<void> complete(PurchaseUpdate update) async {
    if (update.token != null) completed.add(update.token!);
  }
}
