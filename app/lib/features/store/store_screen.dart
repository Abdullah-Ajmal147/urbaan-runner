import 'package:flutter/material.dart';
import 'package:urban_runner/core/widgets/phosphor.dart';
import 'package:provider/provider.dart';

import '../../core/app_services.dart';
import '../../core/theme/tokens.dart';
import '../../core/widgets/widgets.dart';
import '../../games/urban_runner/game_state.dart';
import '../../games/urban_runner/items.dart';
import 'purchases.dart';

class StoreScreen extends StatefulWidget {
  const StoreScreen({super.key});
  @override
  State<StoreScreen> createState() => _StoreScreenState();
}

class _StoreScreenState extends State<StoreScreen> {
  Map<String, String> _prices = const {};

  @override
  void initState() {
    super.initState();
    _loadPrices();
  }

  Future<void> _loadPrices() async {
    final store = context.read<AppServices>().store;
    try {
      final products = await store.products({Products.removeAds, Products.starterPack});
      if (mounted) setState(() => _prices = {for (final p in products) p.id: p.price});
    } catch (_) {
      // Store unavailable (no Play services / offline): buttons say so.
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<GameState>();
    final purchases = context.watch<Purchases>();
    final p = state.profile;
    final starterOwned = p.ownedItems.contains(Items.lantern.id);

    String priceLabel(String id) => _prices[id] ?? 'Unavailable';

    return SafeArea(
      bottom: false,
      child: ListView(padding: const EdgeInsets.fromLTRB(16, 12, 16, 24), children: [
        Row(children: [
          const Text('Store', style: UrText.title),
          const Spacer(),
          TextButton(
            onPressed: purchases.busy ? null : () => purchases.restore(),
            child: Text('Restore purchases', style: UrText.label.copyWith(color: UrColors.subtle, decoration: TextDecoration.underline)),
          ),
        ]),
        const SizedBox(height: 12),
        ClipRRect(
          borderRadius: BorderRadius.circular(UrRadius.hero),
          child: Container(
            height: 400,
            color: UrColors.amber,
            child: Stack(children: [
              Positioned(right: -90, bottom: -40, child: Container(width: 300, height: 300, decoration: const BoxDecoration(shape: BoxShape.circle, color: Color(0xFFFFC873)))),
              Positioned(right: -52, bottom: -30, width: 250, child: Image.asset('assets/images/dex.png', semanticLabel: 'Dex')),
              Positioned(
                left: 20,
                top: 20,
                child: Container(
                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                  decoration: BoxDecoration(color: UrColors.amberInk, borderRadius: BorderRadius.circular(8)),
                  child: Text(starterOwned ? 'OWNED' : 'ONE-TIME OFFER', style: UrText.small.copyWith(color: UrColors.amber, fontWeight: FontWeight.w800, letterSpacing: .8)),
                ),
              ),
              Positioned(left: 20, top: 62, child: Text('Starter\nPack', style: UrText.hero.copyWith(fontSize: 48, height: .9, letterSpacing: -2, color: UrColors.amberInk))),
              Positioned(
                left: 20,
                top: 176,
                width: 170,
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Text('2,000', style: UrText.hero.copyWith(fontSize: 26, color: UrColors.amberInk)),
                  Text('sparks to spend', style: UrText.label.copyWith(color: UrColors.amberInk)),
                  const SizedBox(height: 12),
                  Container(height: 1, color: UrColors.amberInk.withValues(alpha: .2)),
                  const SizedBox(height: 12),
                  Text('Lantern trail', style: UrText.cardTitle.copyWith(fontSize: 20, color: UrColors.amberInk)),
                  Text('only in this pack', style: UrText.label.copyWith(color: UrColors.amberInk)),
                ]),
              ),
              Positioned(
                left: 20,
                bottom: 20,
                width: 170,
                child: PressButton(
                  key: const Key('buy_starter'),
                  label: starterOwned ? 'Owned' : priceLabel(Products.starterPack),
                  semanticsLabel: starterOwned ? 'Starter Pack owned' : 'Buy the Starter Pack for ${priceLabel(Products.starterPack)}',
                  color: UrColors.amberInk,
                  shade: Colors.black,
                  ink: UrColors.amber,
                  height: 54,
                  onPressed: starterOwned || purchases.busy || !_prices.containsKey(Products.starterPack)
                      ? null
                      : () => purchases.buy(Products.starterPack, consumable: false),
                ),
              ),
            ]),
          ),
        ),
        const SizedBox(height: 16),
        Panel(
          padding: const EdgeInsets.all(18),
          child: Column(children: [
            Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Container(
                width: 44,
                height: 44,
                decoration: BoxDecoration(color: UrColors.magenta, borderRadius: BorderRadius.circular(12)),
                child: Icon(PhosphorIcons.shieldCheck(PhosphorIconsStyle.fill), color: UrColors.magentaInk, size: 22),
              ),
              const SizedBox(width: 14),
              const Expanded(
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Text('Remove Ads', style: UrText.cardTitle),
                  SizedBox(height: 4),
                  Text('No ads between runs, ever. You can still choose a reward ad for a revive or double sparks.', style: UrText.body),
                ]),
              ),
            ]),
            const SizedBox(height: 16),
            PressButton(
              key: const Key('buy_remove_ads'),
              label: p.removeAds ? 'Ads removed' : priceLabel(Products.removeAds),
              semanticsLabel: p.removeAds ? 'Ads removed' : 'Remove ads for ${priceLabel(Products.removeAds)}',
              height: 52,
              onPressed: p.removeAds || purchases.busy || !_prices.containsKey(Products.removeAds)
                  ? null
                  : () => purchases.buy(Products.removeAds, consumable: false),
            ),
          ]),
        ),
        const SizedBox(height: 18),
        const Text('Payments go through Google Play. Outfits and trails are for looks only, they never change how you play.', style: UrText.small, textAlign: TextAlign.center),
      ]),
    );
  }
}
