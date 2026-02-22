/**
 * Level configuration passed from SurviveMode.razor to JS.
 */
export interface SurviveLevelData {
  level?: number;
  Level?: number;

  biomeName?: string;
  BiomeName?: string;

  timerDurationSeconds?: number;
  TimerDurationSeconds?: number;

  baseEnemyCount?: number;
  BaseEnemyCount?: number;
  maxEnemyCount?: number;
  MaxEnemyCount?: number;
  spawnIntervalSeconds?: number;
  SpawnIntervalSeconds?: number;

  enemySpeed?: number;
  EnemySpeed?: number;
  maxEnemySpeed?: number;
  MaxEnemySpeed?: number;
  playerSpeed?: number;
  PlayerSpeed?: number;

  enemyScale?: number;
  EnemyScale?: number;

  hasEliteEnemies?: boolean;
  HasEliteEnemies?: boolean;
  eliteSpawnChance?: number;
  EliteSpawnChance?: number;

  mapWidth?: number;
  MapWidth?: number;
  mapHeight?: number;
  MapHeight?: number;
  viewportWidth?: number;
  ViewportWidth?: number;
  viewportHeight?: number;
  ViewportHeight?: number;

  backgroundPath?: string;
  BackgroundPath?: string;
  enemySprites?: string[];
  EnemySprites?: string[];
  playerSpritePath?: string;
  PlayerSpritePath?: string;
  bossSprites?: string[];
  BossSprites?: string[];

  isFinalLevel?: boolean;
  IsFinalLevel?: boolean;

  spawnRampPerMinute?: number;
  SpawnRampPerMinute?: number;
  speedRampPerMinute?: number;
  SpeedRampPerMinute?: number;
}
