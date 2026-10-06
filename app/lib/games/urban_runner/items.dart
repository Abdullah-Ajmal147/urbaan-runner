/// Locker items (HANDOVER §3.7): cosmetic only, they never change stats.
enum ItemKind { character, trail }

enum UnlockKind { free, sparks, level, iap }

class LockerItem {
  const LockerItem({
    required this.id,
    required this.kind,
    required this.name,
    required this.tagline,
    required this.unlock,
    this.price = 0,
    this.level = 0,
    this.image,
    required this.accent,
    required this.ink,
  });

  final String id;
  final ItemKind kind;
  final String name;
  final String tagline;
  final UnlockKind unlock;
  final int price;
  final int level;
  final String? image;
  final int accent;
  final int ink;
}

abstract final class Items {
  static const mira = LockerItem(
    id: 'mira', kind: ItemKind.character, name: 'Mira', tagline: 'Knows every shortcut through the night market',
    unlock: UnlockKind.free, image: 'assets/images/mira.png', accent: 0xFFFF3DA6, ink: 0xFF14051F);
  static const dex = LockerItem(
    id: 'dex', kind: ItemKind.character, name: 'Dex', tagline: 'Never drops a parcel, never misses a jump',
    unlock: UnlockKind.sparks, price: 1500, image: 'assets/images/dex.png', accent: 0xFF2EE6FF, ink: 0xFF062A31);
  static const juno = LockerItem(
    id: 'juno', kind: ItemKind.character, name: 'Juno', tagline: 'Joins the crew at Courier Level 10',
    unlock: UnlockKind.level, level: 10, image: 'assets/images/juno.png', accent: 0xFFFFB547, ink: 0xFF2B1700);

  static const neonRibbon = LockerItem(
    id: 'trail_neon', kind: ItemKind.trail, name: 'Neon Ribbon', tagline: 'Pink to blue, all the way down the street',
    unlock: UnlockKind.free, accent: 0xFFFF3DA6, ink: 0xFF14051F);
  static const paperKite = LockerItem(
    id: 'trail_kite', kind: ItemKind.trail, name: 'Paper Kite', tagline: 'A tail of tiny paper kites',
    unlock: UnlockKind.level, level: 8, accent: 0xFFFFB547, ink: 0xFF2B1700);
  static const lantern = LockerItem(
    id: 'trail_lantern', kind: ItemKind.trail, name: 'Lantern', tagline: 'Only in the Starter Pack',
    unlock: UnlockKind.iap, accent: 0xFFFFB547, ink: 0xFF2B1700);

  static const characters = [mira, dex, juno];
  static const trails = [neonRibbon, paperKite, lantern];
  static const all = [...characters, ...trails];

  static LockerItem byId(String id) => all.firstWhere((i) => i.id == id, orElse: () => mira);

  static const defaultOwned = {'mira', 'trail_neon'};
}

enum PowerUp { magnet, shield, double }

abstract final class Upgrades {
  static const maxLevel = 5;

  /// Sparks to buy the next level (index = current level).
  static const costs = [300, 600, 900, 1400, 2000];

  static int? nextCost(int level) => level >= maxLevel ? null : costs[level];

  /// Duration in seconds at a level (HANDOVER §3.5), shown in the Locker.
  static double duration(PowerUp p, int level) => switch (p) {
        PowerUp.magnet => 8 + 1.5 * level,
        PowerUp.shield => 20 + 3.0 * level,
        PowerUp.double => 10 + 2.0 * level,
      };

  static String label(PowerUp p) => switch (p) {
        PowerUp.magnet => 'Magnet',
        PowerUp.shield => 'Shield',
        PowerUp.double => 'Double Sparks',
      };

  static String key(PowerUp p) => switch (p) {
        PowerUp.magnet => 'magnet',
        PowerUp.shield => 'shield',
        PowerUp.double => 'double',
      };
}
