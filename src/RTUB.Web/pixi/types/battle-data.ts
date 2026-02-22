/**
 * Data shapes passed from Blazor to JS via IJSRuntime.InvokeVoidAsync.
 */

/** Spell definition from the server. */
export interface SpellDefinition {
  attackId?: string;
  AttackId?: string;
  name?: string;
  Name?: string;
  icon?: string;
  Icon?: string;
  cooldownSeconds?: number;
  CooldownSeconds?: number;
}

/** Enemy combatant passed for interactive mode. */
export interface EnemyDefinition {
  hp?: number;
  HP?: number;
  maxHP?: number;
  MaxHP?: number;
  actionTime?: number;
  ActionTime?: number;
}

/** Data passed to `window.myTunoGame.startBattle(hostId, data)`. */
export interface ArenaBattleData {
  // Events (pre-computed replay) or null for interactive
  EventsJson?: string;
  eventsJson?: string;

  // DotNet interop reference
  dotNetRef?: DotNet.DotNetObject;
  DotNetRef?: DotNet.DotNetObject;

  // Combatant names
  attackerName?: string;
  AttackerName?: string;
  defenderName?: string;
  DefenderName?: string;

  // Gameplay config
  battleSpeed?: number;
  BattleSpeed?: number;
  HasShotBuff?: boolean;
  hasShotBuff?: boolean;

  // Interactive mode
  InteractiveMode?: boolean;
  interactiveMode?: boolean;
  Spells?: SpellDefinition[];
  spells?: SpellDefinition[];
  PlayerHP?: number;
  playerHP?: number;
  PlayerMaxHP?: number;
  playerMaxHP?: number;
  PlayerActionTime?: number;
  playerActionTime?: number;
  Enemies?: EnemyDefinition[];
  enemies?: EnemyDefinition[];
}

/** Consumable quantities map. */
export interface ConsumableQuantities {
  fino?: number;
  Fino?: number;
  caneca?: number;
  Caneca?: number;
  cigarro?: number;
  Cigarro?: number;
  canhao?: number;
  Canhao?: number;
  shot?: number;
  Shot?: number;
  penalty?: number;
  Penalty?: number;
}

/** Consumable image URLs map. */
export interface ConsumableImages {
  fino?: string;
  Fino?: string;
  caneca?: string;
  Caneca?: string;
  cigarro?: string;
  Cigarro?: string;
  canhao?: string;
  Canhao?: string;
}

/** Active buff flags. */
export interface ActiveBuffs {
  cigarro?: boolean;
  Cigarro?: boolean;
  canhao?: boolean;
  Canhao?: boolean;
  shot?: boolean;
  Shot?: boolean;
  penalty?: boolean;
  Penalty?: boolean;
}

/** Data passed to `window.stageBattleGame.start(hostId, data)` and `nextBattle(data)`. */
export interface StageBattleData {
  // Events
  EventsJson?: string;
  eventsJson?: string;

  // DotNet
  dotNetRef?: DotNet.DotNetObject;
  DotNetRef?: DotNet.DotNetObject;

  // Stage info
  StageNumber?: number;
  stageNumber?: number;
  EnemyType?: string;
  enemyType?: string;
  EnemyCount?: number;
  enemyCount?: number;
  PlayerName?: string;
  playerName?: string;
  EnemyName?: string;
  enemyName?: string;

  // Visuals
  BackgroundPath?: string;
  backgroundPath?: string;
  PlayerSpritePath?: string;
  playerSpritePath?: string;
  EnemySprites?: string[];
  enemySprites?: string[];
  EnemyPlacements?: number[];
  enemyPlacements?: number[];

  // Gameplay
  BattleSpeed?: number;
  battleSpeed?: number;
  HasShotBuff?: boolean;
  hasShotBuff?: boolean;
  HasPenaltyBuff?: boolean;
  hasPenaltyBuff?: boolean;

  // Interactive
  InteractiveMode?: boolean;
  interactiveMode?: boolean;
  Spells?: SpellDefinition[];
  spells?: SpellDefinition[];
  PlayerHP?: number;
  playerHP?: number;
  PlayerMaxHP?: number;
  playerMaxHP?: number;
  PlayerActionTime?: number;
  playerActionTime?: number;
  Enemies?: EnemyDefinition[];
  enemies?: EnemyDefinition[];

  // Consumables
  Consumables?: ConsumableQuantities;
  consumables?: ConsumableQuantities;
  ConsumableImages?: ConsumableImages;
  consumableImages?: ConsumableImages;
  ConsumableCooldowns?: Record<string, number>;
  consumableCooldowns?: Record<string, number>;
  SpellCooldowns?: Record<string, number>;
  spellCooldowns?: Record<string, number>;
  ActiveBuffs?: ActiveBuffs;
  activeBuffs?: ActiveBuffs;
}
