/**
 * Village mode data types — in-memory state for Phase 1.
 */

export enum ResourceType {
  Wood = 'wood',
  Clay = 'clay',
  Iron = 'iron',
  Crop = 'crop',
}

export interface ResourceState {
  type: ResourceType;
  amount: number;
  /** Production per second */
  production: number;
  /** Max storage capacity */
  capacity: number;
}

export enum BuildingType {
  TownHall = 'townHall',
  LumberMill = 'lumberMill',
  ClayPit = 'clayPit',
  IronMine = 'ironMine',
  Farm = 'farm',
}

export interface UpgradeCost {
  wood: number;
  clay: number;
  iron: number;
  crop: number;
}

export interface BuildingSlot {
  id: number;
  type: BuildingType;
  level: number;
  /** Normalized X position (0–1) relative to scene center */
  x: number;
  /** Normalized Y position (0–1) relative to scene center */
  y: number;
  name: string;
  description: string;
  upgradeCost: UpgradeCost;
}

export interface VillageState {
  buildings: BuildingSlot[];
  resources: ResourceState[];
  villageLevel: number;
}

/** Data pushed to Blazor on each resource tick */
export interface ResourceTickData {
  wood: number;
  clay: number;
  iron: number;
  crop: number;
}
