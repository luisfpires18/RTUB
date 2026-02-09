-- =============================================================
-- DEPRECATED: SeedData.StageEnemies.cs now handles upsert on startup.
-- No need to run this SQL manually — bosses are auto-migrated to
-- new 1000-stage-per-biome intervals by the seed code.
-- =============================================================
-- Stage Enemies: Mountains + Snowy Regions
-- SQLite compatible - Run via DB razor page tool
-- EnemyType: Normal=0, Boss=2
-- RegionType: Mountains=2, Snowy=3
-- PlacementType: Terrestrial=0, Aerial=1
-- =============================================================

-- =====================
-- MOUNTAINS - 10 normal enemies
-- =====================

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Goat', 0, 2, 70, 9, 6, 7, 0.05, '/sprites/games/my-tuno/enemies/mountains/goat.png', '1.0', 0.1, 0.05, datetime('now'), NULL, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Hound', 0, 2, 55, 12, 9, 4, 0.05, '/sprites/games/my-tuno/enemies/mountains/hound.png', '1.0', 0.1, 0.05, datetime('now'), NULL, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Hyena', 0, 2, 60, 13, 8, 3, 0.05, '/sprites/games/my-tuno/enemies/mountains/hyena.png', '1.0', 0.1, 0.05, datetime('now'), NULL, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Lynx', 0, 2, 50, 14, 10, 3, 0.06, '/sprites/games/my-tuno/enemies/mountains/lynx.png', '1.0', 0.1, 0.05, datetime('now'), NULL, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Monkey', 0, 2, 45, 10, 11, 3, 0.05, '/sprites/games/my-tuno/enemies/mountains/monkey.png', '1.0', 0.1, 0.05, datetime('now'), NULL, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Elephant', 0, 2, 95, 11, 3, 10, 0.05, '/sprites/games/my-tuno/enemies/mountains/elephant.png', '1.0', 0.1, 0.05, datetime('now'), NULL, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Rhino', 0, 2, 85, 15, 4, 9, 0.05, '/sprites/games/my-tuno/enemies/mountains/rhino.png', '1.0', 0.1, 0.05, datetime('now'), NULL, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Bear', 0, 2, 80, 13, 5, 8, 0.05, '/sprites/games/my-tuno/enemies/mountains/bear.png', '1.0', 0.1, 0.05, datetime('now'), NULL, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Vulture', 0, 2, 40, 11, 12, 2, 0.06, '/sprites/games/my-tuno/enemies/mountains/vulture.png', '1.0', 0.1, 0.05, datetime('now'), NULL, 1);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Pigeon', 0, 2, 35, 7, 13, 1, 0.05, '/sprites/games/my-tuno/enemies/mountains/pigeon.png', '1.0', 0.1, 0.05, datetime('now'), NULL, 1);

-- =====================
-- MOUNTAINS - 10 bosses (stages 210, 220, ..., 300)
-- =====================

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Alpine Guardian', 2, 2, 185, 24, 5, 18, 0.10, '/sprites/games/my-tuno/enemies/mountains/boss_1_alpine.png', '1.0', 0.1, 0.05, datetime('now'), 210, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Alpaca Chief', 2, 2, 200, 28, 7, 20, 0.12, '/sprites/games/my-tuno/enemies/mountains/boss_2_alpaca.png', '1.0', 0.1, 0.05, datetime('now'), 220, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Bull Titan', 2, 2, 190, 34, 10, 15, 0.15, '/sprites/games/my-tuno/enemies/mountains/boss_3_bull.png', '1.0', 0.1, 0.05, datetime('now'), 230, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Giraffe Sage', 2, 2, 195, 32, 12, 16, 0.14, '/sprites/games/my-tuno/enemies/mountains/boss_4_giraffe.png', '1.0', 0.1, 0.05, datetime('now'), 240, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Snow Leopard', 2, 2, 235, 26, 6, 24, 0.08, '/sprites/games/my-tuno/enemies/mountains/boss_5_leopard.png', '1.0', 0.1, 0.05, datetime('now'), 250, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Mountain Fox', 2, 2, 220, 36, 8, 20, 0.12, '/sprites/games/my-tuno/enemies/mountains/boss_6_fox.png', '1.0', 0.1, 0.05, datetime('now'), 260, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Alpha Wolf', 2, 2, 250, 30, 9, 26, 0.10, '/sprites/games/my-tuno/enemies/mountains/boss_7_wolf.png', '1.0', 0.1, 0.05, datetime('now'), 270, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Harpy', 2, 2, 230, 40, 11, 22, 0.18, '/sprites/games/my-tuno/enemies/mountains/boss_8_harpy.png', '1.0', 0.1, 0.05, datetime('now'), 280, 1);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Mountain Gorilla', 2, 2, 280, 34, 6, 28, 0.12, '/sprites/games/my-tuno/enemies/mountains/boss_9_gorilla.png', '1.0', 0.1, 0.05, datetime('now'), 290, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Stone Golem', 2, 2, 340, 45, 10, 35, 0.20, '/sprites/games/my-tuno/enemies/mountains/boss_10_golem.png', '1.0', 0.1, 0.05, datetime('now'), 300, 0);

-- =====================
-- SNOWY - 10 normal enemies
-- =====================

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Wolf', 0, 3, 65, 12, 8, 5, 0.05, '/sprites/games/my-tuno/enemies/snowy/wolf.png', '1.0', 0.1, 0.05, datetime('now'), NULL, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Fox', 0, 3, 45, 10, 11, 3, 0.06, '/sprites/games/my-tuno/enemies/snowy/fox.png', '1.0', 0.1, 0.05, datetime('now'), NULL, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Bear', 0, 3, 90, 14, 4, 9, 0.05, '/sprites/games/my-tuno/enemies/snowy/bear.png', '1.0', 0.1, 0.05, datetime('now'), NULL, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Owl', 0, 3, 40, 9, 12, 2, 0.06, '/sprites/games/my-tuno/enemies/snowy/owl.png', '1.0', 0.1, 0.05, datetime('now'), NULL, 1);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Panda', 0, 3, 85, 11, 5, 10, 0.05, '/sprites/games/my-tuno/enemies/snowy/panda.png', '1.0', 0.1, 0.05, datetime('now'), NULL, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Penguin', 0, 3, 50, 8, 7, 6, 0.05, '/sprites/games/my-tuno/enemies/snowy/penguin.png', '1.0', 0.1, 0.05, datetime('now'), NULL, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Rabbit', 0, 3, 35, 7, 14, 2, 0.05, '/sprites/games/my-tuno/enemies/snowy/rabbit.png', '1.0', 0.1, 0.05, datetime('now'), NULL, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Reindeer', 0, 3, 75, 10, 8, 7, 0.05, '/sprites/games/my-tuno/enemies/snowy/reindeer.png', '1.0', 0.1, 0.05, datetime('now'), NULL, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Snowman', 0, 3, 70, 8, 3, 12, 0.05, '/sprites/games/my-tuno/enemies/snowy/snowman.png', '1.0', 0.1, 0.05, datetime('now'), NULL, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Mammoth', 0, 3, 100, 13, 3, 11, 0.05, '/sprites/games/my-tuno/enemies/snowy/mammoth.png', '1.0', 0.1, 0.05, datetime('now'), NULL, 0);

-- =====================
-- SNOWY - 10 bosses (stages 310, 320, ..., 400)
-- =====================

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Ice Golem', 2, 3, 195, 26, 5, 20, 0.10, '/sprites/games/my-tuno/enemies/snowy/boss_1_golem.png', '1.0', 0.1, 0.05, datetime('now'), 310, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Frost Snowman', 2, 3, 215, 30, 7, 22, 0.12, '/sprites/games/my-tuno/enemies/snowy/boss_2_snoman.png', '1.0', 0.1, 0.05, datetime('now'), 320, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Winter Owl', 2, 3, 200, 36, 10, 17, 0.15, '/sprites/games/my-tuno/enemies/snowy/boss_3_owl.png', '1.0', 0.1, 0.05, datetime('now'), 330, 1);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Panda Warlord', 2, 3, 210, 34, 12, 18, 0.14, '/sprites/games/my-tuno/enemies/snowy/boss_4_panda.png', '1.0', 0.1, 0.05, datetime('now'), 340, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Saber Tiger', 2, 3, 250, 28, 6, 26, 0.08, '/sprites/games/my-tuno/enemies/snowy/boss_5_tiger.png', '1.0', 0.1, 0.05, datetime('now'), 350, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Emperor Penguin', 2, 3, 235, 38, 8, 22, 0.12, '/sprites/games/my-tuno/enemies/snowy/boss_6_penguin.png', '1.0', 0.1, 0.05, datetime('now'), 360, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Yeti', 2, 3, 265, 32, 9, 28, 0.10, '/sprites/games/my-tuno/enemies/snowy/boss_7_yeti.png', '1.0', 0.1, 0.05, datetime('now'), 370, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Dire Wolf', 2, 3, 245, 42, 11, 24, 0.18, '/sprites/games/my-tuno/enemies/snowy/boss_8_direwolf.png', '1.0', 0.1, 0.05, datetime('now'), 380, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Ancient Mammoth', 2, 3, 300, 36, 6, 30, 0.12, '/sprites/games/my-tuno/enemies/snowy/boss_9_mammoth.png', '1.0', 0.1, 0.05, datetime('now'), 390, 0);

INSERT INTO "StageEnemies" ("Name", "Type", "Region", "BaseHP", "BasePower", "BaseSpeed", "BaseDefense", "BaseCriticalChance", "SpritePath", "BaseFidelisDrop", "BeerDropChance", "ShotDropChance", "CreatedAt", "BossStageNumber", "Placement")
VALUES ('Frost Drake', 2, 3, 365, 48, 10, 38, 0.20, '/sprites/games/my-tuno/enemies/snowy/boss_10_drake.png', '1.0', 0.1, 0.05, datetime('now'), 400, 0);
