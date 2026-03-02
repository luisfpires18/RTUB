/**
 * Server response shapes from [JSInvokable] Blazor methods.
 */
import type { BattleEvent } from './battle-events';

/** Returned by OnPlayerAutoAttack, OnEnemyAttack. */
export interface CombatActionResult {
  events?: BattleEvent[];
  Events?: BattleEvent[];
  battleOver?: boolean;
  BattleOver?: boolean;
  outcome?: CombatOutcome;
  Outcome?: CombatOutcome;
}

/** Combat outcome enum. */
export const enum CombatOutcome {
  AttackerWon = 0,
  DefenderWon = 1,
  Draw = 2,
}

/** Returned by OnUseConsumable. */
export interface ConsumableResult {
  success?: boolean;
  Success?: boolean;
  newQuantity?: number;
  NewQuantity?: number;
  cooldownSeconds?: number;
  CooldownSeconds?: number;
  playerHP?: number;
  PlayerHP?: number;
  playerMaxHP?: number;
  PlayerMaxHP?: number;
  healAmount?: number;
  HealAmount?: number;
  buffMessage?: string;
  BuffMessage?: string;
  buffActive?: boolean;
  BuffActive?: boolean;
  newActionTime?: number;
  NewActionTime?: number;
  message?: string;
  Message?: string;
}

/** Returned by OnTickConsumableCooldowns. */
export type CooldownMap = Record<string, number>;
