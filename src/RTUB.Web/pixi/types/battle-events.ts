/**
 * Battle event types from the C# DeterministicCombatEngine / CombatActionService.
 */

/** All possible event type strings emitted by the combat system. */
export type BattleEventType =
  | 'BattleStart'
  | 'Attack'
  | 'HPUpdate'
  | 'KO'
  | 'Victory';

/**
 * A single battle event as emitted by the server (JSON-serialised).
 * Properties may arrive in PascalCase or camelCase depending on the serialiser.
 */
export interface BattleEvent {
  // Core
  Type?: BattleEventType;
  type?: BattleEventType;

  Attacker?: string;
  attacker?: string;

  Defender?: string;
  defender?: string;

  Damage?: number;
  damage?: number;

  IsCritical?: boolean;
  isCritical?: boolean;

  IsBlocked?: boolean;
  isBlocked?: boolean;

  IsDodged?: boolean;
  isDodged?: boolean;

  IsBoosted?: boolean;
  isBoosted?: boolean;

  SimTime?: number;
  simTime?: number;

  // HP state
  Character?: string;
  character?: string;

  HP?: number;
  hp?: number;

  MaxHP?: number;
  maxHP?: number;

  ActionTime?: number;
  actionTime?: number;

  // Victory / KO
  Winner?: string;
  winner?: string;

  VisualHint?: string;
  visualHint?: string;
}
