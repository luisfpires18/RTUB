/**
 * Village mode constants — building definitions, layout, colors.
 */
import { BuildingType, ResourceType } from '@village-types/village-data';
import type { BuildingSlot, ResourceState, UpgradeCost } from '@village-types/village-data';

/* ═══════════════════════════════════════════
   Color Palette
   ═══════════════════════════════════════════ */

export const COLORS = {
  // Ground & environment
  groundGreen: 0x4a7c3f,
  groundDark: 0x3a6330,
  pathTan: 0xc4a96a,
  pathBorder: 0x9e8854,
  water: 0x5b9bd5,

  // Building bases
  townHall: 0xd4a843,
  lumberMill: 0x8b6914,
  clayPit: 0xc2703a,
  ironMine: 0x7a8b99,
  farm: 0x6b9e3a,

  // Building roofs
  townHallRoof: 0xb8922e,
  lumberMillRoof: 0x6b5010,
  clayPitRoof: 0x9e5a2e,
  ironMineRoof: 0x5a6a77,
  farmRoof: 0x557e2e,

  // UI
  badgeBg: 0xffffff,
  badgeText: '#333333',
  badgeBorder: 0x666666,
  labelText: '#ffffff',
  labelShadow: '#000000',
  shadowColor: 0x000000,

  // Resource icons
  woodIcon: 0x8b6914,
  clayIcon: 0xc2703a,
  ironIcon: 0x7a8b99,
  cropIcon: 0x6b9e3a,

  // Tree decorations
  treeTrunk: 0x6b4226,
  treeLeaves: 0x3a7a2a,
  treeLeavesLight: 0x4a9a3a,

  // Popup
  popupBg: 0x1a1a2e,
  popupBorder: 0xd4a843,
  popupText: '#ffffff',
  popupClose: 0xff4444,
  upgradeBtn: 0x4caf50,
  upgradeBtnHover: 0x66cc6a,
  disabledBtn: 0x666666,
} as const;

/* ═══════════════════════════════════════════
   Building Definitions
   ═══════════════════════════════════════════ */

function cost(wood: number, clay: number, iron: number, crop: number): UpgradeCost {
  return { wood, clay, iron, crop };
}

/**
 * Default buildings for a new village. Positions are pixel offsets from scene center.
 */
export function createDefaultBuildings(): BuildingSlot[] {
  return [
    {
      id: 0,
      type: BuildingType.TownHall,
      level: 1,
      x: 0,
      y: 0,
      name: 'Centro da Vila',
      description: 'Coração da vila. Sobe o nível máximo dos outros edifícios.',
      upgradeCost: cost(100, 100, 80, 60),
    },
    {
      id: 1,
      type: BuildingType.LumberMill,
      level: 1,
      x: -200,
      y: -140,
      name: 'Serralharia',
      description: 'Produz madeira para construção.',
      upgradeCost: cost(50, 30, 20, 10),
    },
    {
      id: 2,
      type: BuildingType.ClayPit,
      level: 1,
      x: 200,
      y: -140,
      name: 'Cova de Barro',
      description: 'Extrai barro para materiais de construção.',
      upgradeCost: cost(30, 50, 20, 10),
    },
    {
      id: 3,
      type: BuildingType.IronMine,
      level: 1,
      x: -200,
      y: 140,
      name: 'Mina de Ferro',
      description: 'Extrai ferro para ferramentas e armas.',
      upgradeCost: cost(30, 20, 50, 10),
    },
    {
      id: 4,
      type: BuildingType.Farm,
      level: 1,
      x: 200,
      y: 140,
      name: 'Quinta',
      description: 'Produz alimento para alimentar a vila.',
      upgradeCost: cost(20, 20, 10, 50),
    },
  ];
}

/* ═══════════════════════════════════════════
   Resource Defaults
   ═══════════════════════════════════════════ */

export function createDefaultResources(): ResourceState[] {
  return [
    { type: ResourceType.Wood, amount: 500, production: 5, capacity: 2000 },
    { type: ResourceType.Clay, amount: 500, production: 5, capacity: 2000 },
    { type: ResourceType.Iron, amount: 500, production: 5, capacity: 2000 },
    { type: ResourceType.Crop, amount: 500, production: 5, capacity: 2000 },
  ];
}

/* ═══════════════════════════════════════════
   Building Colors Map
   ═══════════════════════════════════════════ */

export const BUILDING_COLORS: Record<BuildingType, { base: number; roof: number }> = {
  [BuildingType.TownHall]: { base: COLORS.townHall, roof: COLORS.townHallRoof },
  [BuildingType.LumberMill]: { base: COLORS.lumberMill, roof: COLORS.lumberMillRoof },
  [BuildingType.ClayPit]: { base: COLORS.clayPit, roof: COLORS.clayPitRoof },
  [BuildingType.IronMine]: { base: COLORS.ironMine, roof: COLORS.ironMineRoof },
  [BuildingType.Farm]: { base: COLORS.farm, roof: COLORS.farmRoof },
};

/* ═══════════════════════════════════════════
   Building Icons (Bootstrap Icons codepoints)
   ═══════════════════════════════════════════ */

export const BUILDING_ICONS: Record<BuildingType, string> = {
  [BuildingType.TownHall]: '🏛️',
  [BuildingType.LumberMill]: '🪵',
  [BuildingType.ClayPit]: '🧱',
  [BuildingType.IronMine]: '⛏️',
  [BuildingType.Farm]: '🌾',
};

/* ═══════════════════════════════════════════
   Resource Display
   ═══════════════════════════════════════════ */

export const RESOURCE_ICONS: Record<ResourceType, string> = {
  [ResourceType.Wood]: '🪵',
  [ResourceType.Clay]: '🧱',
  [ResourceType.Iron]: '⛏️',
  [ResourceType.Crop]: '🌾',
};

export const RESOURCE_NAMES: Record<ResourceType, string> = {
  [ResourceType.Wood]: 'Madeira',
  [ResourceType.Clay]: 'Barro',
  [ResourceType.Iron]: 'Ferro',
  [ResourceType.Crop]: 'Alimento',
};

/* ═══════════════════════════════════════════
   Production per level
   ═══════════════════════════════════════════ */

/** Production per second at a given building level */
export function productionPerSecond(level: number): number {
  return Math.round(5 * Math.pow(1.4, level - 1) * 100) / 100;
}

/** Capacity at a given town hall level  */
export function storageCapacity(townHallLevel: number): number {
  return 2000 + (townHallLevel - 1) * 1000;
}

/** Upgrade cost multiplier per level */
export function upgradeCostForLevel(baseCost: UpgradeCost, level: number): UpgradeCost {
  const mult = Math.pow(1.5, level - 1);
  return {
    wood: Math.round(baseCost.wood * mult),
    clay: Math.round(baseCost.clay * mult),
    iron: Math.round(baseCost.iron * mult),
    crop: Math.round(baseCost.crop * mult),
  };
}
