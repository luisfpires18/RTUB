-- =============================================================
-- DEPRECATED: SeedData.StageEnemies.cs now handles upsert on startup.
-- No need to run this SQL manually — bosses are auto-migrated to
-- new 1000-stage-per-biome intervals by the seed code.
-- =============================================================
-- Stage Enemies: New Forest Normals + Full Swamp Region
-- SQLite compatible - Run via DB razor page tool
-- EnemyType: Normal=0, Boss=2
-- RegionType: Forest=0, Swamp=1
-- PlacementType: Terrestrial=0
-- =============================================================

-- =====================
-- FOREST - 2 missing normals (Cheetah, Stag)
-- =====================

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Cheetah', 0, 0, 55, 13, 12, 3, 0.05, '/sprites/games/my-tuno/enemies/forest/cheetah.png', '1.0', 0.1, 0.05, datetime('now'), NULL, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Stag', 0, 0, 75, 9, 7, 6, 0.05, '/sprites/games/my-tuno/enemies/forest/stag.png', '1.0', 0.1, 0.05, datetime('now'), NULL, 0);

-- =====================
-- SWAMP - 10 normal enemies
-- =====================

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Crab', 0, 1, 65, 10, 4, 8, 0.05, '/sprites/games/my-tuno/enemies/swamp/crab.png', '1.0', 0.1, 0.05, datetime('now'), NULL, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Crocodile', 0, 1, 90, 14, 3, 7, 0.05, '/sprites/games/my-tuno/enemies/swamp/crocodile.png', '1.0', 0.1, 0.05, datetime('now'), NULL, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Crow', 0, 1, 40, 9, 11, 2, 0.05, '/sprites/games/my-tuno/enemies/swamp/crow.png', '1.0', 0.1, 0.05, datetime('now'), NULL, 1);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Frog', 0, 1, 35, 7, 9, 3, 0.05, '/sprites/games/my-tuno/enemies/swamp/frog.png', '1.0', 0.1, 0.05, datetime('now'), NULL, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Leech', 0, 1, 45, 12, 6, 2, 0.05, '/sprites/games/my-tuno/enemies/swamp/leech.png', '1.0', 0.1, 0.05, datetime('now'), NULL, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Mosquito', 0, 1, 30, 8, 13, 1, 0.05, '/sprites/games/my-tuno/enemies/swamp/mosquito.png', '1.0', 0.1, 0.05, datetime('now'), NULL, 1);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Salamander', 0, 1, 55, 11, 7, 5, 0.05, '/sprites/games/my-tuno/enemies/swamp/salamander.png', '1.0', 0.1, 0.05, datetime('now'), NULL, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Slime', 0, 1, 80, 6, 2, 10, 0.05, '/sprites/games/my-tuno/enemies/swamp/slime.png', '1.0', 0.1, 0.05, datetime('now'), NULL, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Snake', 0, 1, 50, 10, 8, 3, 0.05, '/sprites/games/my-tuno/enemies/swamp/snake.png', '1.0', 0.1, 0.05, datetime('now'), NULL, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Stalker', 0, 1, 60, 13, 10, 4, 0.05, '/sprites/games/my-tuno/enemies/swamp/stalker.png', '1.0', 0.1, 0.05, datetime('now'), NULL, 0);

-- =====================
-- SWAMP - 10 bosses (stages 110-200)
-- All bosses have sprites
-- =====================

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Frog King', 2, 1, 170, 24, 5, 18, 0.10, '/sprites/games/my-tuno/enemies/swamp/boss_1_frog.png', '60', 0.5, 0.2, datetime('now'), 110, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Pelican', 2, 1, 185, 28, 7, 20, 0.12, '/sprites/games/my-tuno/enemies/swamp/boss_2_pelican.png', '70', 0.5, 0.2, datetime('now'), 120, 1);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Leech Lord', 2, 1, 175, 34, 10, 15, 0.15, '/sprites/games/my-tuno/enemies/swamp/boss_3_leech.png', '80', 0.5, 0.2, datetime('now'), 130, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Hydra', 2, 1, 180, 32, 12, 16, 0.14, '/sprites/games/my-tuno/enemies/swamp/boss_4_hydra.png', '90', 0.5, 0.2, datetime('now'), 140, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Anaconda', 2, 1, 215, 26, 6, 24, 0.08, '/sprites/games/my-tuno/enemies/swamp/boss_5_anaconda.png', '100', 0.5, 0.2, datetime('now'), 150, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Crayfish', 2, 1, 200, 36, 8, 20, 0.12, '/sprites/games/my-tuno/enemies/swamp/boss_6_crayfish.png', '110', 0.5, 0.2, datetime('now'), 160, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Darner', 2, 1, 230, 30, 9, 26, 0.10, '/sprites/games/my-tuno/enemies/swamp/boss_7_darner.png', '120', 0.5, 0.2, datetime('now'), 170, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Hippopotamus', 2, 1, 210, 40, 11, 22, 0.18, '/sprites/games/my-tuno/enemies/swamp/boss_8_hippopotamus.png', '130', 0.5, 0.2, datetime('now'), 180, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Troll', 2, 1, 260, 34, 6, 28, 0.12, '/sprites/games/my-tuno/enemies/swamp/boss_9_troll.png', '140', 0.5, 0.2, datetime('now'), 190, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Aligator', 2, 1, 320, 45, 10, 35, 0.20, '/sprites/games/my-tuno/enemies/swamp/boss_10_aligator.png', '250', 0.8, 0.4, datetime('now'), 200, 0);

-- =====================
-- FIX: Update bosses 4-9 that already exist with NULL sprites and old names
-- Run these even if INSERTs above fail (UNIQUE constraint)
-- =====================
UPDATE "StageEnemies" SET "Name" = 'Hydra', "SpritePath" = '/sprites/games/my-tuno/enemies/swamp/boss_4_hydra.png' WHERE "BossStageNumber" = 140 AND "Region" = 1;
UPDATE "StageEnemies" SET "Name" = 'Anaconda', "SpritePath" = '/sprites/games/my-tuno/enemies/swamp/boss_5_anaconda.png' WHERE "BossStageNumber" = 150 AND "Region" = 1;
UPDATE "StageEnemies" SET "Name" = 'Crayfish', "SpritePath" = '/sprites/games/my-tuno/enemies/swamp/boss_6_crayfish.png' WHERE "BossStageNumber" = 160 AND "Region" = 1;
UPDATE "StageEnemies" SET "Name" = 'Darner', "SpritePath" = '/sprites/games/my-tuno/enemies/swamp/boss_7_darner.png' WHERE "BossStageNumber" = 170 AND "Region" = 1;
UPDATE "StageEnemies" SET "Name" = 'Hippopotamus', "SpritePath" = '/sprites/games/my-tuno/enemies/swamp/boss_8_hippopotamus.png' WHERE "BossStageNumber" = 180 AND "Region" = 1;
UPDATE "StageEnemies" SET "Name" = 'Troll', "SpritePath" = '/sprites/games/my-tuno/enemies/swamp/boss_9_troll.png' WHERE "BossStageNumber" = 190 AND "Region" = 1;

-- =====================
-- FIX: Set Aerial placement for flying enemies
-- Run these even if INSERTs above were already applied with Placement=0
-- =====================
UPDATE "StageEnemies" SET "Placement" = 1 WHERE "Region" = 1 AND "Name" = 'Crow' AND "Type" = 0;
UPDATE "StageEnemies" SET "Placement" = 1 WHERE "Region" = 1 AND "Name" = 'Mosquito' AND "Type" = 0;
UPDATE "StageEnemies" SET "Placement" = 1 WHERE "Region" = 1 AND "Name" = 'Pelican' AND "Type" = 2;
