-- Fix forest enemy placements that were not updated when the Placement column was added.
-- The AddPlacementToStageEnemy migration added the column with defaultValue: 0 (Terrestrial),
-- so pre-existing forest aerial enemies (Bee, Beetle, Eagle) remained at 0.
-- The seed code only inserts new enemies, it doesn't update existing ones.
-- Run this script on production to fix the placement values.

-- Forest normal enemies that should be Aerial (Placement = 1)
UPDATE "StageEnemies" SET "Placement" = 1 WHERE "Region" = 0 AND "Name" = 'Bee' AND "Type" = 0;
UPDATE "StageEnemies" SET "Placement" = 1 WHERE "Region" = 0 AND "Name" = 'Beetle' AND "Type" = 0;
UPDATE "StageEnemies" SET "Placement" = 1 WHERE "Region" = 0 AND "Name" = 'Eagle' AND "Type" = 0;

-- Forest boss that should be Aerial (Placement = 1)
UPDATE "StageEnemies" SET "Placement" = 1 WHERE "Region" = 0 AND "Name" = 'Falcon' AND "Type" = 2;
